# 🔧 GitHub Environment Setup for CI/CD

This document explains how to configure GitHub environments and secrets for the Conduit CI/CD pipeline.

## 🌟 Required Environments

### 1. `nuget-publishing` Environment

This environment is used for publishing packages to NuGet.org and requires approval.

#### Setup Steps:

1. **Go to Repository Settings**
   - Navigate to your repository
   - Click "Settings" → "Environments"
   - Click "New environment"
   - Name: `nuget-publishing`

2. **Configure Environment Protection Rules**
   - ✅ **Required reviewers**: Add yourself or trusted maintainers
   - ✅ **Wait timer**: 0 minutes (or add delay if desired)
   - ✅ **Deployment branches**: `main` and `master` only

3. **Add Environment Secrets**
   - Click "Add secret"
   - Name: `NUGET_API_KEY`
   - Value: Your NuGet.org API key (create at https://www.nuget.org/account/apikeys)

## 🔑 Required Secrets

### Repository-Level Secrets

These secrets are automatically available to all workflows:

| Secret Name | Description | Where to Get It |
|-------------|-------------|-----------------|
| `GITHUB_TOKEN` | ✅ **Auto-provided** | Automatically created by GitHub |

### Environment-Level Secrets

| Environment | Secret Name | Description | Where to Get It |
|-------------|-------------|-------------|-----------------|
| `nuget-publishing` | `NUGET_API_KEY` | NuGet.org API key for package publishing | [NuGet.org Account](https://www.nuget.org/account/apikeys) |

## 🚀 Setting Up NuGet API Key

1. **Create NuGet.org Account**
   - Go to https://www.nuget.org
   - Sign up or log in

2. **Generate API Key**
   - Go to https://www.nuget.org/account/apikeys
   - Click "Create"
   - Name: `Conduit Framework CI/CD`
   - Select Package Owner: (your account)
   - Scopes:
     - ✅ **Push**: Push new packages and package versions
     - ✅ **Push new packages**: Push new packages (first version of any package)
   - Glob Pattern: `Conduit.*` (restrict to Conduit packages only)

3. **Copy the API Key**
   - ⚠️ **Important**: Copy the key immediately - it won't be shown again
   - Store it securely

4. **Add to GitHub Environment**
   - Repository Settings → Environments → `nuget-publishing`
   - Add secret: `NUGET_API_KEY`
   - Paste the API key

## 🛡️ Security Best Practices

### API Key Security
- ✅ **Scope Limitation**: Only grant necessary permissions
- ✅ **Package Restriction**: Use glob patterns to limit packages
- ✅ **Environment Protection**: Require approval for sensitive operations
- ✅ **Regular Rotation**: Update API keys periodically

### Branch Protection
- ✅ **Protected Branches**: Only allow releases from `master`/`main`
- ✅ **Required Reviews**: Require PR reviews before merging
- ✅ **Status Checks**: Require CI to pass before merge

## 🔄 Workflow Triggers

### Continuous Integration (`ci.yml`)
```yaml
on:
  push:
    branches: [ master, develop ]
  pull_request:
    branches: [ master ]
```

### Release Workflow (`release.yml`)
```yaml
on:
  push:
    tags:
      - 'v*.*.*'      # Stable releases (v1.0.0)
      - 'v*.*.*-*'    # Pre-releases (v1.0.0-alpha)
```

### Security Scanning (`codeql.yml`)
```yaml
on:
  push:
    branches: [ "master", "develop" ]
  pull_request:
    branches: [ "master" ]
  schedule:
    - cron: '30 1 * * 2'  # Weekly
```

## 📦 Release Process

### Automated Release (Recommended)
1. **Create and Push Tag**
   ```bash
   git tag v0.9.0-alpha
   git push origin v0.9.0-alpha
   ```

2. **Workflow Execution**
   - ✅ CI validates build and tests
   - ✅ Packages are created automatically
   - ✅ GitHub release is created with release notes
   - ⏳ **Manual approval required** for NuGet publishing
   - ✅ Packages published to NuGet.org

### Manual Release (Backup)
```bash
# Build packages locally
./scripts/build-packages.sh

# Publish to NuGet.org
dotnet nuget push ./packages/*.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## 🔍 Monitoring and Troubleshooting

### Check Workflow Status
- Repository → Actions tab
- View individual workflow runs
- Check logs for any failures

### Common Issues

1. **NuGet API Key Invalid**
   - Error: `401 Unauthorized`
   - Solution: Regenerate API key and update secret

2. **Package Already Exists**
   - Error: `409 Conflict`
   - Solution: Increment version number

3. **Environment Not Found**
   - Error: `Environment 'nuget-publishing' not found`
   - Solution: Create environment as described above

### Getting Help
- 📚 [GitHub Actions Documentation](https://docs.github.com/en/actions)
- 📦 [NuGet.org Documentation](https://docs.microsoft.com/en-us/nuget/)
- 🔍 Check workflow logs for detailed error messages

---

Once these environments are configured, your CI/CD pipeline will be fully automated! 🚀