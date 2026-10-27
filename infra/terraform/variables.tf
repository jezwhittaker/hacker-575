variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "uksouth"
}

variable "environment" {
  description = "Short environment name used in resource names and tags."
  type        = string
  default     = "dev"
}

variable "app_name" {
  description = "Base name for resources."
  type        = string
  default     = "octocat"
}

variable "image_tag" {
  description = "Tag of the api and frontend images in ACR (the workflow passes the git SHA)."
  type        = string
  default     = "latest"
}

variable "payment_provider" {
  description = "Payment provider used by the API (PAYMENT_PROVIDER). Only 'simulated' is implemented."
  type        = string
  default     = "simulated"
}

variable "payment_currency" {
  description = "ISO currency code used for payments (PAYMENT_CURRENCY)."
  type        = string
  default     = "USD"
}

variable "payment_api_key" {
  description = "Optional payment provider API key (PAYMENT_API_KEY). Supply via TF_VAR_payment_api_key, never in source."
  type        = string
  default     = ""
  sensitive   = true
}
