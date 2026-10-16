# -----------------------------------------------------------------------------
# Hosted MCP server (OnCallHelperMcp in HTTP mode), same shape as the API:
# container image on Lambda -> API Gateway HTTP API -> CloudFront on
# mcp.oncallhelper.com. It reuses the existing wildcard ACM certificate and
# the API's variables; everything else is its own resource so the two apps
# cannot affect each other (separate role, repo, logs, gateway, distribution).
#
# DNS is at GoDaddy: after `terraform apply`, add a CNAME  mcp -> the
# `mcp_cloudfront_domain` output (see Infra/README.md).
# -----------------------------------------------------------------------------

locals {
  mcp_name = "${var.mcp_app_name}-${var.environment}"

  ssm_parameter_arns_mcp = [
    for name in [
      var.mcp_auth0_client_id_parameter_name,
      var.mcp_auth0_client_secret_parameter_name,
      var.mcp_allowed_user_ids_parameter_name
    ] : "arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter${name}"
  ]

  # api_endpoint looks like https://xxxx.execute-api.us-east-1.amazonaws.com
  mcp_origin_domain = replace(aws_apigatewayv2_api.mcp.api_endpoint, "https://", "")
}

# ---- Image repository ---------------------------------------------------------

resource "aws_ecr_repository" "mcp" {
  name = local.mcp_name

  image_scanning_configuration {
    scan_on_push = true
  }
}

# ---- IAM: its own role; it can read ONLY the MCP parameters --------------------
# (The API role can read the OpenAI key and Mongo connection string; the MCP
# server only calls the API, so it must not be able to.)

resource "aws_iam_role" "mcp_lambda_role" {
  name               = "${local.mcp_name}-lambda-role"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume_role.json
}

data "aws_iam_policy_document" "mcp_lambda_policy" {
  statement {
    sid = "CloudWatchLogs"

    actions = [
      "logs:CreateLogStream",
      "logs:PutLogEvents"
    ]

    resources = [
      "${aws_cloudwatch_log_group.mcp.arn}:*"
    ]
  }

  statement {
    sid = "ReadSpecificSsmParameters"

    actions = [
      "ssm:GetParameter"
    ]

    resources = local.ssm_parameter_arns_mcp
  }

  statement {
    sid = "DecryptParameters"

    actions = [
      "kms:Decrypt"
    ]

    resources = ["*"]
  }
}

resource "aws_iam_policy" "mcp_lambda_policy" {
  name   = "${local.mcp_name}-lambda-policy"
  policy = data.aws_iam_policy_document.mcp_lambda_policy.json
}

resource "aws_iam_role_policy_attachment" "mcp_lambda_policy_attach" {
  role       = aws_iam_role.mcp_lambda_role.name
  policy_arn = aws_iam_policy.mcp_lambda_policy.arn
}

# ---- Logs ---------------------------------------------------------------------

resource "aws_cloudwatch_log_group" "mcp" {
  name              = "/aws/lambda/${local.mcp_name}"
  retention_in_days = 14
}

# ---- Lambda -------------------------------------------------------------------

resource "aws_lambda_function" "mcp" {
  function_name = local.mcp_name
  role          = aws_iam_role.mcp_lambda_role.arn
  package_type  = "Image"
  image_uri     = "${aws_ecr_repository.mcp.repository_url}:${var.mcp_image_tag}"

  memory_size = 1024
  timeout     = 30

  architectures = ["x86_64"]

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT = var.environment

      # Who may connect: Auth0 tokens for this audience, from allowlisted users only.
      MCP_PUBLIC_URL             = "https://${var.mcp_custom_domain}"
      MCP_AUTHORITY              = var.auth0_authority
      MCP_ALLOWED_USER_IDS_PARAM = var.mcp_allowed_user_ids_parameter_name

      # How it calls the API (machine-to-machine token from the existing M2M app).
      ONCALL_API_BASE_URL              = "https://${var.api_custom_domain}"
      ONCALL_AUTH0_DOMAIN              = trimsuffix(replace(var.auth0_authority, "https://", ""), "/")
      ONCALL_AUTH0_AUDIENCE            = var.auth0_audience
      ONCALL_AUTH0_CLIENT_ID_PARAM     = var.mcp_auth0_client_id_parameter_name
      ONCALL_AUTH0_CLIENT_SECRET_PARAM = var.mcp_auth0_client_secret_parameter_name
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.mcp
  ]

  lifecycle {
    precondition {
      condition     = var.mcp_image_tag != "unset"
      error_message = "Push the MCP image to ECR and set mcp_image_tag in terraform.tfvars first (see Infra/README.md)."
    }
  }
}

# ---- API Gateway (HTTP API) ---------------------------------------------------

resource "aws_apigatewayv2_api" "mcp" {
  name          = "${local.mcp_name}-http"
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_integration" "mcp_lambda_integration" {
  api_id                 = aws_apigatewayv2_api.mcp.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.mcp.invoke_arn
  payload_format_version = "2.0"
}

resource "aws_apigatewayv2_route" "mcp_root" {
  api_id    = aws_apigatewayv2_api.mcp.id
  route_key = "ANY /"
  target    = "integrations/${aws_apigatewayv2_integration.mcp_lambda_integration.id}"
}

resource "aws_apigatewayv2_route" "mcp_proxy" {
  api_id    = aws_apigatewayv2_api.mcp.id
  route_key = "ANY /{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.mcp_lambda_integration.id}"
}

resource "aws_apigatewayv2_stage" "mcp_default" {
  api_id      = aws_apigatewayv2_api.mcp.id
  name        = "$default"
  auto_deploy = true

  # Cap request rate before the Lambda runs (excess gets 429). Best-effort target, see variables.tf.
  default_route_settings {
    throttling_rate_limit  = var.mcp_throttle_rate_limit
    throttling_burst_limit = var.mcp_throttle_burst_limit
  }
}

resource "aws_lambda_permission" "mcp_allow_api_gateway" {
  statement_id  = "AllowExecutionFromApiGateway"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.mcp.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.mcp.execution_arn}/*/*"
}

# ---- CloudFront on mcp.oncallhelper.com (reuses the wildcard certificate) -----

resource "aws_cloudfront_distribution" "mcp" {
  enabled     = true
  comment     = "${local.mcp_name} MCP server"
  aliases     = [var.mcp_custom_domain]
  price_class = "PriceClass_100" # cheapest: North America + Europe edges

  origin {
    domain_name = local.mcp_origin_domain
    origin_id   = "apigw-mcp"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  default_cache_behavior {
    target_origin_id       = "apigw-mcp"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS", "PUT", "POST", "PATCH", "DELETE"]
    cached_methods         = ["GET", "HEAD"]

    # Same AWS managed policies as the API distribution:
    #  - CachingDisabled
    #  - AllViewerExceptHostHeader (forwards Authorization and query strings)
    cache_policy_id          = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad"
    origin_request_policy_id = "b689b0a8-53d0-40ab-baf2-68738e2966ac"
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    acm_certificate_arn      = aws_acm_certificate_validation.cf.certificate_arn
    ssl_support_method       = "sni-only"
    minimum_protocol_version = "TLSv1.2_2021"
  }
}

# ---- Outputs ------------------------------------------------------------------

output "mcp_ecr_repository_url" {
  value = aws_ecr_repository.mcp.repository_url
}

output "mcp_lambda_function_name" {
  value = aws_lambda_function.mcp.function_name
}

# Point mcp.oncallhelper.com at this via a CNAME in GoDaddy.
output "mcp_cloudfront_domain" {
  value = aws_cloudfront_distribution.mcp.domain_name
}

output "mcp_public_url" {
  value = "https://${var.mcp_custom_domain}"
}
