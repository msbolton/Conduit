#!/bin/bash

# Conduit Framework Package Build Script
# Builds all NuGet packages for the Conduit framework

set -e

echo "🏗️  Building Conduit Framework Packages..."

# Clean previous builds
echo "🧹 Cleaning previous builds..."
dotnet clean --configuration Release

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Build all projects
echo "🔨 Building solution..."
dotnet build --configuration Release --no-restore

# Run tests to ensure quality
echo "🧪 Running tests..."
dotnet test --configuration Release --no-build --verbosity normal

# Create packages
echo "📋 Creating NuGet packages..."
dotnet pack --configuration Release --no-build --output ./packages

# List created packages
echo "✅ Packages created:"
ls -la ./packages/*.nupkg

echo ""
echo "🎉 Package build complete!"
echo ""
echo "To publish to NuGet.org:"
echo "dotnet nuget push ./packages/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json"
echo ""
echo "To publish to local feed:"
echo "dotnet nuget push ./packages/*.nupkg --source /path/to/local/feed"