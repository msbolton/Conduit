# Version Management Strategy

## Semantic Versioning

Conduit follows [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH (e.g., 0.1.0, 1.2.3, 2.0.0)
```

### Version Components

- **MAJOR**: Incompatible API changes (breaking changes)
- **MINOR**: New functionality in a backward-compatible manner
- **PATCH**: Backward-compatible bug fixes

### Pre-1.0.0 Development

**Current Stage**: `0.1.0` (Initial Development)

During 0.x versions:
- **0.x.0**: New modules or major features added
- **0.x.y**: Bug fixes and minor improvements
- Breaking changes are allowed without bumping MAJOR

**When to reach 1.0.0**:
- All planned core modules complete (23/23 modules)
- Production-ready and tested
- API stable and documented
- At least one real-world deployment

## When to Bump Versions

### PATCH (0.1.X) - Bump When:

✅ **Bug Fixes**
```
fix(messaging): Correct message correlation ID null handling
fix(security): Fix token expiration timezone issue
```

✅ **Documentation Updates**
```
docs: Fix typo in README installation steps
docs(api): Clarify ICommand interface usage
```

✅ **Performance Improvements** (no API change)
```
perf(serialization): Optimize JSON deserialization speed
```

✅ **Internal Refactoring** (no external API change)
```
refactor(core): Simplify component registry lookup
```

**Example**: `0.1.0` → `0.1.1` → `0.1.2`

### MINOR (0.X.0) - Bump When:

✅ **New Modules Added**
```
feat(resilience): Add circuit breaker and retry policies
feat(transports): Add AMQP transport implementation
```

✅ **New Features** (backward-compatible)
```
feat(messaging): Add message priority routing
feat(security): Add OAuth2 authentication provider
```

✅ **Deprecations** (mark for future removal)
```
feat(api): Add IMessageBusV2, deprecate IMessageBus
```

**Example**: `0.1.0` → `0.2.0` → `0.3.0`

### MAJOR (X.0.0) - Bump When:

✅ **Breaking API Changes**
```
feat(core)!: Change component lifecycle state machine

BREAKING CHANGE: Component states reduced from 13 to 8.
Update all component implementations.
```

✅ **Removed Deprecated Features**
```
feat!: Remove deprecated IMessageBus interface
```

✅ **Architecture Changes**
```
refactor!: Migrate from sync to async-only APIs
```

**Example**: `0.9.0` → `1.0.0` → `2.0.0`

**Note**: For 0.x versions, breaking changes increment MINOR instead of MAJOR.

## Commit-to-Version Mapping

### Single Commit Types

| Commit Type | Version Bump | Example |
|-------------|--------------|---------|
| `fix` | PATCH | `0.1.0` → `0.1.1` |
| `perf` | PATCH | `0.1.0` → `0.1.1` |
| `feat` | MINOR | `0.1.0` → `0.2.0` |
| `feat!` | MAJOR* | `0.1.0` → `0.2.0` (0.x) or `1.0.0` → `2.0.0` |
| `docs`, `style`, `refactor`, `test`, `chore` | NO BUMP | `0.1.0` → `0.1.0` |

*During 0.x, breaking changes bump MINOR

### Multiple Commits Between Releases

The **highest** priority change determines the version:

```bash
# Example: 0.1.0 → ?
fix(messaging): Fix null reference in handler
docs: Update README
feat(security): Add 2FA support
fix(api): Correct interface documentation

# Result: 0.1.0 → 0.2.0 (MINOR wins)
```

**Priority**: `feat!` > `feat` > `fix`/`perf` > others

## Version Bump Workflow

### 1. Determine Next Version

```bash
# Count commits since last tag by type
git log v0.1.0..HEAD --oneline --no-decorate

# Check for breaking changes
git log v0.1.0..HEAD --grep="BREAKING CHANGE"

# Check for features
git log v0.1.0..HEAD --grep="^feat"

# Check for fixes
git log v0.1.0..HEAD --grep="^fix"
```

### 2. Update Version Files

Update these files with the new version:

```bash
# VERSION file
echo "0.2.0" > VERSION

# All .csproj files
find src -name "*.csproj" -exec sed -i 's/<Version>0.1.0<\/Version>/<Version>0.2.0<\/Version>/g' {} \;

# README.md badge
sed -i 's/version-0.1.0/version-0.2.0/g' README.md

# Update CHANGELOG.md (see CHANGELOG.md section below)
```

### 3. Create Version Bump Commit

```bash
git add VERSION README.md CHANGELOG.md src/*/Conduit.*.csproj
git commit -m "chore(release): Bump version to 0.2.0

- Add Conduit.Resilience module
- Add Conduit.Transports.Core module
- Fix message correlation issues
- Update documentation"
```

### 4. Tag the Release

```bash
git tag -a v0.2.0 -m "Release version 0.2.0

Features:
- Add circuit breaker and retry policies
- Add transport abstraction layer

Bug Fixes:
- Fix message correlation tracking
- Fix token expiration handling

See CHANGELOG.md for full details"

git push origin develop --tags
```

## Branching Strategy (Git Flow)

### Branch Types

```
master (or main)
  ├── develop
  │   ├── feature/add-resilience-module
  │   ├── feature/add-kafka-transport
  │   └── fix/message-correlation-bug
  ├── release/0.2.0
  └── hotfix/0.1.1-security-fix
```

### Branch Lifecycle

#### 1. **master** - Production Releases
- Contains only released code
- Every commit is a version tag
- Never commit directly
- Only merge from `release/*` or `hotfix/*`

#### 2. **develop** - Integration Branch
- Active development happens here
- All features merge here first
- Should always be in working state
- Nightly builds run from here

#### 3. **feature/*** - New Features
- Branch from: `develop`
- Merge to: `develop`
- Naming: `feature/add-circuit-breaker`, `feature/kafka-transport`

```bash
# Create feature branch
git checkout develop
git checkout -b feature/add-resilience-module

# Work on feature with atomic commits
git add src/Conduit.Resilience/CircuitBreaker.cs
git commit -m "feat(resilience): Add circuit breaker implementation"

git add src/Conduit.Resilience/RetryPolicy.cs
git commit -m "feat(resilience): Add retry policy with exponential backoff"

# Merge to develop
git checkout develop
git merge --no-ff feature/add-resilience-module -m "feat(resilience): Add resilience module

- Circuit breaker with state management
- Retry policies with exponential backoff
- Bulkhead isolation
- Timeout policies"

git push origin develop
git branch -d feature/add-resilience-module
```

#### 4. **release/X.Y.Z** - Release Preparation
- Branch from: `develop`
- Merge to: `master` AND `develop`
- Naming: `release/0.2.0`
- Only bug fixes, version bumps, and documentation

```bash
# Create release branch
git checkout develop
git checkout -b release/0.2.0

# Bump version
echo "0.2.0" > VERSION
find src -name "*.csproj" -exec sed -i 's/<Version>0.1.0<\/Version>/<Version>0.2.0<\/Version>/g' {} \;
sed -i 's/version-0.1.0/version-0.2.0/g' README.md

# Update CHANGELOG
# (Edit CHANGELOG.md to finalize release notes)

git add VERSION README.md CHANGELOG.md src/*/Conduit.*.csproj
git commit -m "chore(release): Bump version to 0.2.0"

# Last-minute fixes only
git commit -m "fix(release): Correct package metadata"

# Merge to master
git checkout master
git merge --no-ff release/0.2.0 -m "Release version 0.2.0"
git tag -a v0.2.0 -m "Release 0.2.0"

# Merge back to develop
git checkout develop
git merge --no-ff release/0.2.0 -m "Merge release 0.2.0 back to develop"

# Cleanup
git branch -d release/0.2.0
git push origin master develop --tags
```

#### 5. **hotfix/X.Y.Z** - Emergency Fixes
- Branch from: `master`
- Merge to: `master` AND `develop`
- Naming: `hotfix/0.1.1-security-vulnerability`
- For critical production bugs

```bash
# Create hotfix branch
git checkout master
git checkout -b hotfix/0.1.1-security-fix

# Fix the issue
git commit -m "fix(security)!: Patch JWT validation vulnerability

CVE-2025-XXXXX: Token signature validation bypass

BREAKING CHANGE: Tokens signed with weak algorithms now rejected"

# Bump PATCH version
echo "0.1.1" > VERSION
find src -name "*.csproj" -exec sed -i 's/<Version>0.1.0<\/Version>/<Version>0.1.1<\/Version>/g' {} \;

git add VERSION src/*/Conduit.*.csproj
git commit -m "chore(release): Bump version to 0.1.1"

# Merge to master
git checkout master
git merge --no-ff hotfix/0.1.1-security-fix
git tag -a v0.1.1 -m "Hotfix 0.1.1: Security vulnerability patch"

# Merge to develop
git checkout develop
git merge --no-ff hotfix/0.1.1-security-fix

# Cleanup
git branch -d hotfix/0.1.1-security-fix
git push origin master develop --tags
```

## When to Create Release Branches

### ✅ Create Release Branch When:

1. **Sufficient features accumulated** (typically 3-5 new modules or major features)
2. **Scheduled release date approaching** (e.g., monthly releases)
3. **Major milestone reached** (e.g., all core modules complete)
4. **Stability checkpoint needed** (feature freeze for testing)

### Example Release Timeline:

```
Week 1-2: Feature development on develop
  ├── feat(resilience): Circuit breaker
  ├── feat(transports): AMQP adapter
  └── feat(transports): gRPC adapter

Week 3: Create release/0.2.0 branch
  ├── Code freeze
  ├── Integration testing
  ├── Bug fixes
  └── Documentation updates

Week 4: Release
  ├── Merge to master
  ├── Tag v0.2.0
  └── Deploy/publish
```

## CHANGELOG Maintenance

Update `CHANGELOG.md` with each release:

```markdown
## [0.2.0] - 2025-10-30

### Added
- **Conduit.Resilience** - Circuit breaker, retry policies, bulkhead, timeout
- **Conduit.Transports.Core** - Transport abstraction layer
- **Conduit.Transports.Amqp** - RabbitMQ transport implementation

### Changed
- Improved message correlation performance by 40%
- Updated MessageBus to support priority queues

### Fixed
- Fixed null reference in message handler registry
- Corrected token expiration timezone handling

### Deprecated
- `IMessageBus.Send()` - Use `IMessageBus.SendAsync()` instead

[0.2.0]: https://github.com/conduit/conduit-dotnet/compare/v0.1.0...v0.2.0
```

## Automated Versioning (Future)

Consider these tools for automation:

### Option 1: semantic-release
```bash
npm install --save-dev semantic-release
```

### Option 2: GitVersion
```bash
dotnet tool install --global GitVersion.Tool
```

### Option 3: Custom Script
Create `scripts/bump-version.sh`:

```bash
#!/bin/bash
# Auto-detect version bump from commit messages
LAST_TAG=$(git describe --tags --abbrev=0)
COMMITS=$(git log $LAST_TAG..HEAD --oneline)

if echo "$COMMITS" | grep -q "BREAKING CHANGE"; then
  # Bump MAJOR (or MINOR for 0.x)
elif echo "$COMMITS" | grep -q "^feat"; then
  # Bump MINOR
elif echo "$COMMITS" | grep -q "^fix"; then
  # Bump PATCH
fi
```

## Quick Reference

| Action | Command |
|--------|---------|
| Start feature | `git checkout -b feature/name develop` |
| Finish feature | `git checkout develop && git merge --no-ff feature/name` |
| Start release | `git checkout -b release/0.2.0 develop` |
| Finish release | Merge to `master` and `develop`, tag |
| Emergency fix | `git checkout -b hotfix/0.1.1 master` |
| View version | `cat VERSION` |
| Check commits since release | `git log v0.1.0..HEAD --oneline` |

---

**Remember**: Version numbers are cheap. Don't be afraid to bump them. Users care about stability and clear communication more than version numbers.
