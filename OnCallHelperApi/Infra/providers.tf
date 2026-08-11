provider "aws" {
  region = var.aws_region
}

# CloudFront requires its ACM certificate to be in us-east-1, regardless of
# the API's region. This aliased provider is used only for the certificate.
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"
}