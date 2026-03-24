aws_region  = "us-east-1"
app_name    = "oncall-helper-api"
environment = "prod"
image_tag   = "v1"

auth0_authority = "https://dev-k0sl1xaa1o87ofbn.us.auth0.com/"
auth0_audience  = "http://localhost:5172"

cors_allowed_origins = [
  "https://your-ui.example.com"
]

openai_api_key_parameter_name          = "/oncall-helper/prod/OpenAI__ApiKey"
mongo_connection_string_parameter_name = "/oncall-helper/prod/Mongo__ConnectionString"
mongo_database_parameter_name          = "/oncall-helper/prod/Mongo__Database"