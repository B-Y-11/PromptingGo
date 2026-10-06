namespace PromptHub.Web.Models.Enums;

public enum MediaStorageProvider
{
    Local = 0,

    S3 = 1,

    Minio = 2,

    CloudflareR2 = 3,

    ExternalUrl = 4
}
