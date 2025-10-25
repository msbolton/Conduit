# Conduit Development Cheat Sheet

## Quick Commands

```bash
# Project Info
cat VERSION                          # Current version
git log --oneline -5                 # Recent commits
git branch -a                        # All branches

# Start Feature
git checkout -b feature/name develop

# Commit
git commit -m "type(scope): description"

# Bump Version
./scripts/bump-version.sh [major|minor|patch|X.Y.Z]

# Finish Feature
git checkout develop
git merge --no-ff feature/name
git push origin develop
git branch -d feature/name

# Release
git checkout -b release/X.Y.Z develop
./scripts/bump-version.sh X.Y.Z
# ... update CHANGELOG ...
git checkout master && git merge --no-ff release/X.Y.Z
git tag -a vX.Y.Z -m "Release X.Y.Z"
git checkout develop && git merge --no-ff release/X.Y.Z
git push origin master develop --tags
```

## Commit Types

| Type | Use For | Bump |
|------|---------|------|
| `feat` | New feature | MINOR |
| `fix` | Bug fix | PATCH |
| `docs` | Documentation | - |
| `style` | Formatting | - |
| `refactor` | Code restructure | - |
| `perf` | Performance | PATCH |
| `test` | Tests | - |
| `build` | Build system | - |
| `ci` | CI config | - |
| `chore` | Maintenance | - |
| `revert` | Revert commit | - |

## Version Bumping Rules

```
0.1.0 → 0.1.1  (PATCH)  fix, perf
0.1.0 → 0.2.0  (MINOR)  feat, feat!
0.9.0 → 1.0.0  (MAJOR)  Production ready
1.0.0 → 2.0.0  (MAJOR)  feat! (breaking)
```

## Branch Strategy

```
master              Production (tags only)
  └─ develop        Integration
      ├─ feature/*  New features
      ├─ fix/*      Bug fixes
      └─ ...
  ├─ release/*      Release prep
  └─ hotfix/*       Emergency fixes
```

## When to Release

| Trigger | Type | Timeline |
|---------|------|----------|
| 3-5 features | MINOR | Monthly |
| Bug fixes | PATCH | As needed |
| Security issue | PATCH | Immediate |
| Breaking change | MINOR (0.x) | As needed |
| Breaking change | MAJOR (1.x+) | As needed |

## Conventional Commit Examples

```bash
# Feature
git commit -m "feat(api): Add ICommand interface"

# Bug fix
git commit -m "fix(messaging): Null ref in handler"

# Breaking change
git commit -m "feat(core)!: Change lifecycle API

BREAKING CHANGE: State machine simplified"

# With scope
git commit -m "feat(security): Add JWT provider"
git commit -m "fix(serialization): Handle nulls"
git commit -m "docs(readme): Update install steps"
```

## Release Workflow

```bash
# 1. Develop features
git checkout develop
git merge --no-ff feature/x

# 2. Ready to release
git checkout -b release/0.2.0 develop

# 3. Bump & finalize
./scripts/bump-version.sh 0.2.0
# Edit CHANGELOG.md

# 4. Release
git checkout master
git merge --no-ff release/0.2.0
git tag -a v0.2.0 -m "Release 0.2.0"

# 5. Back to develop
git checkout develop
git merge --no-ff release/0.2.0

# 6. Push
git push origin master develop --tags
git branch -d release/0.2.0
```

## Hotfix Workflow

```bash
# 1. Critical bug in production
git checkout -b hotfix/0.1.1 master

# 2. Fix it
git commit -m "fix(security): Patch CVE-2025-123"

# 3. Bump version
./scripts/bump-version.sh patch

# 4. Merge to master
git checkout master
git merge --no-ff hotfix/0.1.1
git tag -a v0.1.1 -m "Hotfix 0.1.1"

# 5. Merge to develop
git checkout develop
git merge --no-ff hotfix/0.1.1

# 6. Push
git push origin master develop --tags
git branch -d hotfix/0.1.1
```

## Useful Git Commands

```bash
# What's changed since last release?
git log v0.1.0..HEAD --oneline

# Commits by type
git log --oneline | grep "feat"
git log --oneline | grep "fix"

# See a specific commit
git show abc1234

# Undo last commit (keep changes)
git reset --soft HEAD~1

# Undo last commit (discard changes)
git reset --hard HEAD~1

# Fix commit message
git commit --amend -m "new message"

# Tag existing commit
git tag -a v0.1.0 abc1234 -m "Release"

# Delete tag
git tag -d v0.1.0
git push origin :refs/tags/v0.1.0

# Compare branches
git diff develop..feature/name
```

## CHANGELOG Template

```markdown
## [0.2.0] - 2025-10-30

### Added
- New features

### Changed
- Changes to existing

### Deprecated
- Features marked for removal

### Removed
- Deleted features

### Fixed
- Bug fixes

### Security
- Security patches

[0.2.0]: https://github.com/.../compare/v0.1.0...v0.2.0
```

## Build & Test

```bash
# Build
dotnet build

# Test
dotnet test

# Build specific project
dotnet build src/Conduit.Security/

# Pack for NuGet
dotnet pack -c Release

# Restore dependencies
dotnet restore
```

## Quick Reference URLs

- Conventional Commits: https://conventionalcommits.org
- Semantic Versioning: https://semver.org
- Keep a Changelog: https://keepachangelog.com
- Git Flow: https://nvie.com/posts/a-successful-git-branching-model/

---

**Print this and keep it handy! 📋**
