resource "aws_lambda_function" "api" {
  function_name = "${var.app_name}-${var.environment}"
  role          = aws_iam_role.lambda_role.arn
  package_type  = "Image"
  image_uri     = "${aws_ecr_repository.api.repository_url}:${var.image_tag}"

  memory_size = 1024
  timeout     = 30
  
  architectures = ["x86_64"]

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT         = var.environment
      Auth0__Authority               = var.auth0_authority
      Auth0__Audience                = var.auth0_audience

      OPENAI_API_KEY_PARAM           = var.openai_api_key_parameter_name
      MONGO_CONNECTION_STRING_PARAM  = var.mongo_connection_string_parameter_name
      MONGO_DATABASE_PARAM           = var.mongo_database_parameter_name

      Cors__AllowedOrigins__0        = length(var.cors_allowed_origins) > 0 ? var.cors_allowed_origins[0] : ""
      Cors__AllowedOrigins__1        = length(var.cors_allowed_origins) > 1 ? var.cors_allowed_origins[1] : ""
      Cors__AllowedOrigins__2        = length(var.cors_allowed_origins) > 2 ? var.cors_allowed_origins[2] : ""
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda
  ]
}