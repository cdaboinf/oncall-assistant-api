aws_region  = "us-east-1"
app_name    = "oncall-helper-api"
environment = "prod"
image_tag   = "96263fc"

auth0_authority = "https://dev-k0sl1xaa1o87ofbn.us.auth0.com/"
auth0_audience  = "http://localhost:5172"

# CloudFront custom domain (fronts the API on a VPN-friendly hostname)
api_custom_domain = "api.oncallhelper.com"
cert_domain_name  = "*.oncallhelper.com"

# Empty = allow any origin (call the API from anywhere).
# Add specific UI origins here later to lock CORS down, e.g.:
#   cors_allowed_origins = ["https://oncall.example.com"]
cors_allowed_origins = []

openai_api_key_parameter_name          = "/oncall-helper/prod/OpenAI__ApiKey"
mongo_connection_string_parameter_name = "/oncall-helper/prod/Mongo__ConnectionString"
mongo_database_parameter_name          = "/oncall-helper/prod/Mongo__Database"