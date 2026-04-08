# Render deploy notes

## Changed files
- `DGVisionStudio.Api/appsettings.json`
- `DGVisionStudio.Api/appsettings.Development.json`
- `DGVisionStudio.Api/appsettings.Production.json`
- `DGVisionStudio.Api/Program.cs`
- `render.yaml`
- `DGVisionStudio.Api/.env.example`

## Important
- Rotate the old Resend API key immediately.
- Set all secrets only in Render environment variables.
- Uploaded files under `wwwroot/uploads` are not persistent on Render unless you attach a disk or move storage elsewhere.
