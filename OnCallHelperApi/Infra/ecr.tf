resource "aws_ecr_repository" "api" {
  name = "${var.app_name}-${var.environment}"

  image_scanning_configuration {
    scan_on_push = true
  }
}