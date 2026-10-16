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

# ---- Hosted MCP server (mcp.tf) ---------------------------------------------
variable "mcp_app_name" {
  type    = string
  default = "oncall-helper-mcp"
}

# Public hostname of the MCP server. Must equal the Auth0 API identifier
# (https://<this value>) because MCP clients send it as the OAuth `resource`.
variable "mcp_custom_domain" {
  type    = string
  default = "mcp.oncallhelper.com"
}

# Set to the tag you pushed to the MCP ECR repository (see Infra/README.md).
variable "mcp_image_tag" {
  type = string
}

# SSM SecureString parameter NAMES. Values are created by hand with the AWS CLI,
# never in Terraform or git.
variable "mcp_auth0_client_id_parameter_name" {
  type = string
}

variable "mcp_auth0_client_secret_parameter_name" {
  type = string
}

variable "mcp_allowed_user_ids_parameter_name" {
  type = string
}


# ---- Request throttling at the API Gateway stages ------------------------------
# Requests above these limits get a 429 *before* the Lambda runs, which bounds cost and noise.
# API Gateway documents throttles as best-effort targets, and the limit may be applied per route
# (each stage has two routes), so treat the real ceiling as up to ~2x. Raise during an incident
# by changing the value and running plan/apply. Rate = steady requests/second; burst = short spike.
variable "api_throttle_rate_limit" {
  type    = number
  default = 5
}

variable "api_throttle_burst_limit" {
  type    = number
  default = 20
}

variable "mcp_throttle_rate_limit" {
  type    = number
  default = 2
}

variable "mcp_throttle_burst_limit" {
  type    = number
  default = 10
}
