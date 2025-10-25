# Development Workflow Quick Reference

## Daily Development Workflow

### Starting Your Day

```bash
# Update your local develop branch
git checkout develop
git pull origin develop

# Check project status
cat VERSION                    # Current version
git log --oneline -10          # Recent commits
git status                     # Working tree status
```

### Working on a New Feature

```bash
# 1. Create feature branch
git checkout develop
git checkout -b feature/add-redis-cache

# 2. Make atomic commits
git add src/Conduit.Caching/RedisCache.cs
git commit -m "feat(caching): Add Redis cache implementation"

git add src/Conduit.Caching/ICacheProvider.cs
git commit -m "feat(caching): Add cache provider interface"

git add tests/Conduit.Caching.Tests/RedisCacheTests.cs
git commit -m "test(caching): Add Redis cache unit tests"

# 3. Keep branch updated
git checkout develop
git pull origin develop
git checkout feature/add-redis-cache
git rebase develop

# 4. Merge when complete
git checkout develop
git merge --no-ff feature/add-redis-cache -m "feat(caching): Add Redis caching support

- Implement Redis cache provider
- Add cache provider abstraction
- Include comprehensive unit tests"

git push origin develop
git branch -d feature/add-redis-cache
```

### Making a Quick Fix

```bash
# For fixes that go to develop
git checkout develop
git checkout -b fix/null-reference-in-handler

# Make the fix
git add src/Conduit.Messaging/HandlerRegistry.cs
git commit -m "fix(messaging): Prevent null reference in handler lookup

Check for null handler before invocation to prevent
NullReferenceException when handler not found.

Fixes #42"

# Merge to develop
git checkout develop
git merge --no-ff fix/null-reference-in-handler
git push origin develop
git branch -d fix/null-reference-in-handler
```

## Release Workflow

### Regular Release (Minor/Major)

**Timeline: 2-4 weeks of development → Release**

```bash
# Week 1-3: Development on develop branch
git checkout develop
# ... features merged here ...

# Week 4: Create release branch
git checkout develop
git checkout -b release/0.2.0

# Bump version
./scripts/bump-version.sh minor
# This creates: 0.1.0 → 0.2.0

# Update CHANGELOG.md
# Add release notes, categorize changes

git add CHANGELOG.md
git commit -m "docs(changelog): Add release notes for 0.2.0"

# Final testing period (no new features, only bug fixes)
git commit -m "fix(release): Correct package description"

# Ready to release
git checkout master
git merge --no-ff release/0.2.0 -m "Release version 0.2.0"
git tag -a v0.2.0 -m "Release 0.2.0

Features:
- Resilience patterns (circuit breaker, retry)
- Transport layer (AMQP, gRPC)

Bug Fixes:
- Message correlation improvements
- Security token validation fixes

See CHANGELOG.md for details"

# Merge back to develop
git checkout develop
git merge --no-ff release/0.2.0

# Push everything
git push origin master develop --tags

# Cleanup
git branch -d release/0.2.0
```

### Hotfix (Patch Release)

**Timeline: Immediate**

```bash
# Critical bug found in production (master)
git checkout master
git checkout -b hotfix/0.1.1-security-patch

# Fix the critical issue
git add src/Conduit.Security/JwtAuthenticationProvider.cs
git commit -m "fix(security)!: Patch JWT signature validation bypass

CVE-2025-12345: Vulnerability in token signature verification

BREAKING CHANGE: Tokens signed with weak algorithms now rejected.
Update token configuration to use HS256 or stronger."

# Bump patch version
./scripts/bump-version.sh patch
# This creates: 0.1.0 → 0.1.1

# Update CHANGELOG.md
git add CHANGELOG.md
git commit -m "docs(changelog): Add hotfix 0.1.1 notes"

# Merge to master
git checkout master
git merge --no-ff hotfix/0.1.1-security-patch
git tag -a v0.1.1 -m "Hotfix 0.1.1: Security patch"

# Merge to develop
git checkout develop
git merge --no-ff hotfix/0.1.1-security-patch

# Push
git push origin master develop --tags

# Cleanup
git branch -d hotfix/0.1.1-security-patch
```

## When to Release

### Schedule-Based (Recommended for 0.x)

```
Monthly Release Cycle:
├── Week 1-3: Feature development
│   ├── Features merge to develop
│   ├── Continuous integration testing
│   └── Daily builds
└── Week 4: Release preparation
    ├── Create release branch
    ├── Code freeze
    ├── Integration testing
    ├── Bug fixes only
    └── Release

Next month: Repeat
```

### Feature-Based (Alternative)

Release when significant milestones reached:
- ✅ Complete major module (e.g., all transports)
- ✅ 5+ notable features accumulated
- ✅ Breaking changes need to go out
- ✅ Important bug fixes ready

### Hotfix (As Needed)

Release immediately when:
- 🚨 Security vulnerability discovered
- 🚨 Critical production bug
- 🚨 Data corruption issue
- 🚨 Major performance regression

## Version Decision Matrix

| Situation | Action | New Version | Example |
|-----------|--------|-------------|---------|
| Complete new module | MINOR bump | 0.1.0 → 0.2.0 | Add Conduit.Resilience |
| Add backward-compatible feature | MINOR bump | 0.1.0 → 0.2.0 | Add OAuth to Security |
| Fix bug | PATCH bump | 0.1.0 → 0.1.1 | Fix null reference |
| Breaking change (0.x) | MINOR bump | 0.1.0 → 0.2.0 | Change API signature |
| Breaking change (1.x+) | MAJOR bump | 1.2.0 → 2.0.0 | Remove deprecated API |
| Documentation only | NO bump | 0.1.0 → 0.1.0 | Update README |
| Refactor (no API change) | NO bump | 0.1.0 → 0.1.0 | Optimize internals |
| Ready for production | Set to 1.0.0 | 0.9.0 → 1.0.0 | First stable release |

## Common Scenarios

### Scenario 1: Multiple Features Developed

```bash
# After 2 weeks, you have:
- feat(resilience): Circuit breaker
- feat(resilience): Retry policies
- feat(transports): AMQP transport
- fix(messaging): Correlation bug
- docs: Update README

# Decision: MINOR bump (features present)
./scripts/bump-version.sh minor   # 0.1.0 → 0.2.0
```

### Scenario 2: Only Bug Fixes

```bash
# This week you have:
- fix(messaging): Null reference
- fix(security): Token expiration
- perf(serialization): JSON speed
- docs: API documentation

# Decision: PATCH bump (no features)
./scripts/bump-version.sh patch   # 0.1.0 → 0.1.1
```

### Scenario 3: Breaking Change During Development

```bash
# You need to change component lifecycle API
git add src/Conduit.Core/ComponentLifecycleManager.cs
git commit -m "feat(core)!: Simplify component lifecycle states

BREAKING CHANGE: Reduced states from 13 to 8 for simplicity.
Components must update state transition logic.

Migration guide:
- INITIALIZING merged into INITIALIZED
- STARTING merged into RUNNING
- STOPPING merged into STOPPED"

# Decision: MINOR bump in 0.x, would be MAJOR in 1.x+
./scripts/bump-version.sh minor   # 0.1.0 → 0.2.0
```

### Scenario 4: Ready for Production

```bash
# All 23 modules complete, tested in production
# API stable, no major changes planned
# Documentation complete

# Decision: Release 1.0.0
./scripts/bump-version.sh 1.0.0   # 0.9.0 → 1.0.0

# Then follow strict semver:
# - MAJOR for breaking changes
# - MINOR for new features
# - PATCH for bug fixes
```

## Commit Message Examples by Scenario

### Adding a New Module
```bash
git commit -m "feat(resilience): Add circuit breaker pattern

Implement circuit breaker with three states:
- Open: Requests fail immediately
- Half-Open: Test requests allowed
- Closed: Normal operation

Configurable failure threshold and timeout"
```

### Fixing a Bug
```bash
git commit -m "fix(messaging): Prevent duplicate message processing

Add message deduplication using correlation ID cache
to prevent duplicate processing when retries occur.

Fixes #123"
```

### Breaking Change
```bash
git commit -m "feat(api)!: Migrate to async-only message handlers

BREAKING CHANGE: All IMessageHandler methods now async.

Before:
  void Handle(IMessage message)

After:
  Task HandleAsync(IMessage message, CancellationToken ct)

Migration: Add async/await to all handlers"
```

### Documentation
```bash
git commit -m "docs(api): Add XML documentation to all interfaces

- Document all public APIs
- Add code examples
- Include parameter descriptions"
```

## Tagging Conventions

```bash
# Release tags
git tag -a v0.2.0 -m "Release 0.2.0"
git tag -a v1.0.0 -m "Release 1.0.0 - First stable release"

# Pre-release tags (optional, for testing)
git tag -a v0.2.0-rc.1 -m "Release candidate 1 for 0.2.0"
git tag -a v0.2.0-beta.1 -m "Beta 1 for 0.2.0"
git tag -a v0.2.0-alpha.1 -m "Alpha 1 for 0.2.0"
```

## Useful Commands

```bash
# Check what version bump is needed
git log $(git describe --tags --abbrev=0)..HEAD --oneline

# Count commits by type since last tag
git log $(git describe --tags --abbrev=0)..HEAD --oneline | grep -E "^[a-z]+(\([a-z]+\))?" | cut -d: -f1 | sort | uniq -c

# See all tags
git tag -l

# See commits in a tag
git show v0.1.0

# Delete a tag (if mistake)
git tag -d v0.1.0
git push origin :refs/tags/v0.1.0

# Create annotated tag after the fact
git tag -a v0.1.0 abc1234 -m "Release 0.1.0"
```

## GitHub Release Checklist

After tagging:

```bash
# 1. Push tags
git push origin --tags

# 2. Create GitHub release
- Go to GitHub → Releases → Draft new release
- Select tag v0.2.0
- Title: "Conduit Framework v0.2.0"
- Copy CHANGELOG.md section
- Attach build artifacts (optional)
- Click "Publish release"

# 3. Publish NuGet packages (if applicable)
dotnet pack -c Release
dotnet nuget push Conduit.*.nupkg -k $NUGET_API_KEY -s nuget.org
```

---

**Remember**:
- Commit early and often
- Keep commits atomic
- Write meaningful messages
- Version bumps are cheap - use them!
