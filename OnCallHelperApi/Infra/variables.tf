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

variable "openai_api_key_parameter_name" {
  type = string
}

variable "mongo_connection_string_parameter_name" {
  type = string
}

variable "mongo_database_parameter_name" {
  type = string
}