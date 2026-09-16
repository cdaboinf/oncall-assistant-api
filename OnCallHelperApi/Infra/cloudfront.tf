# -----------------------------------------------------------------------------
# CloudFront in front of the API Gateway, on a custom domain
# (api.oncallhelper.com) so corporate VPNs that block *.execute-api.amazonaws.com
# still allow it. CloudFront's free tier covers typical usage.
#
# DNS lives at GoDaddy, so certificate validation and the api CNAME are added
# there manually using the values from `terraform output` (see Infra/README.md).
# -----------------------------------------------------------------------------

# Free ACM certificate (wildcard covers api. and any future subdomains).
# Must be in us-east-1 for CloudFront.
resource "aws_acm_certificate" "cf" {
  provider          = aws.us_east_1
  domain_name       = var.cert_domain_name
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}

# Waits until the certificate is ISSUED. Because DNS is external (GoDaddy), add
# the validation CNAME shown in the `acm_validation_record` output first; this
# resource then completes on the next apply (or once ACM finishes validating).
resource "aws_acm_certificate_validation" "cf" {
  provider        = aws.us_east_1
  certificate_arn = aws_acm_certificate.cf.arn
}

locals {
  # api_endpoint looks like https://xxxx.execute-api.us-east-1.amazonaws.com
  api_origin_domain = replace(aws_apigatewayv2_api.http_api.api_endpoint, "https://", "")
}

resource "aws_cloudfront_distribution" "api" {
  enabled         = true
  comment         = "${var.app_name}-${var.environment} API"
  aliases         = [var.api_custom_domain]
  price_class     = "PriceClass_100" # cheapest: North America + Europe edges

  origin {
    domain_name = local.api_origin_domain
    origin_id   = "apigw"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  default_cache_behavior {
    target_origin_id       = "apigw"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS", "PUT", "POST", "PATCH", "DELETE"]
    cached_methods         = ["GET", "HEAD"]

    # AWS managed policies:
    #  - CachingDisabled: never cache (this is a dynamic API)
    #  - AllViewerExceptHostHeader: forward everything (incl. Authorization &
    #    query strings) but let CloudFront set the Host to the API Gateway origin
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

# ---- Outputs: the two values to paste into GoDaddy DNS -----------------------

# 1) ACM validation CNAME (add this first so the certificate can be issued)
output "acm_validation_record" {
  description = "Add this CNAME in GoDaddy to validate the certificate (Name = strip the trailing .oncallhelper.com)."
  value = {
    type  = tolist(aws_acm_certificate.cf.domain_validation_options)[0].resource_record_type
    name  = tolist(aws_acm_certificate.cf.domain_validation_options)[0].resource_record_name
    value = tolist(aws_acm_certificate.cf.domain_validation_options)[0].resource_record_value
  }
}

# 2) The api record: CNAME api -> this CloudFront domain
output "cloudfront_domain" {
  description = "Point api.oncallhelper.com at this via a CNAME in GoDaddy."
  value       = aws_cloudfront_distribution.api.domain_name
}

output "api_public_url" {
  value = "https://${var.api_custom_domain}"
}
