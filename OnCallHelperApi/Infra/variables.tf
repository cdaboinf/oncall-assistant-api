variable "aws_region" {
  type = string
}

variable "app_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "image_tag" {
  type = string
}

variable "auth0_authority" {
  type = string
}

variable "auth0_audience" {
  type = string
}

variable "cors_allowed_origins" {
  type = list(string)
}

# Custom domain served by CloudFront in front of the API (VPN-friendly hostname).
variable "api_custom_domain" {
  type    = string
  default = "api.oncallhelper.com"
}

# ACM certificate domain. A wildcard covers api. and any future subdomains.
variable "cert_domain_name" {
  type    = string
  default = "*.oncallhelper.com"
}

variable "openai_api_key_parameter_name" {
  type = string
}

variable "mongo_connection_string_parameter_name" {
  type = string
}

variable "mongo_database_parameter_name" {
  type = string
}