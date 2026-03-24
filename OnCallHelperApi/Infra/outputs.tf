output "api_url" {
  value = aws_apigatewayv2_api.http_api.api_endpoint
}

output "ecr_repository_url" {
  value = aws_ecr_repository.api.repository_url
}

output "lambda_function_name" {
  value = aws_lambda_function.api.function_name
}