# Getting Started with Conduit Development

Welcome to the Conduit Framework! This guide will help you start contributing immediately.

## 🚀 Quick Start

### 1. Check Current Status

```bash
cat VERSION                    # Shows: 0.1.0
git branch                     # Shows: develop (active)
git log --oneline -5          # Recent commits
```

### 2. Start Working on a Feature

```bash
# Create feature branch
git checkout -b feature/add-circuit-breaker develop

# Make atomic commits
git add src/Conduit.Resilience/CircuitBreaker.cs
git commit -m "feat(resilience): Add circuit breaker implementation"

# Continue with more commits...
git commit -m "feat(resilience): Add state management"
git commit -m "test(resilience): Add circuit breaker tests"

# Finish feature
git checkout develop
git merge --no-ff feature/add-circuit-breaker
git push origin develop
git branch -d feature/add-circuit-breaker
```

### 3. Fix a Bug

```bash
# Quick fix on develop
git add src/Conduit.Messaging/MessageBus.cs
git commit -m "fix(messaging): Prevent null reference in handler lookup

Check for null handler before invocation to prevent
NullReferenceException when handler not found.

Fixes #42"
```

## 📋 Essential Documentation

| Document | Purpose | When to Use |
|----------|---------|-------------|
| [CHEATSHEET.md](.github/CHEATSHEET.md) | Quick commands | Daily (print it!) |
| [WORKFLOW.md](.github/WORKFLOW.md) | Detailed workflows | Planning releases |
| [VERSIONING.md](.github/VERSIONING.md) | Version strategy | Before bumping version |
| [CONTRIBUTING.md](.github/CONTRIBUTING.md) | Contribution guide | First-time contributors |

## 🎯 Common Tasks

### Adding a New Module

```bash
# 1. Create feature branch
git checkout -b feature/add-resilience-module develop

# 2. Create module structure
mkdir -p src/Conduit.Resilience
cd src/Conduit.Resilience

# 3. Create .csproj file (copy from existing module)
cp ../Conduit.Security/Conduit.Security.csproj Conduit.Resilience.csproj
# Edit to update package info

# 4. Add to solution
dotnet sln add src/Conduit.Resilience/Conduit.Resilience.csproj

# 5. Implement features with atomic commits
git add src/Conduit.Resilience/CircuitBreaker.cs
git commit -m "feat(resilience): Add circuit breaker pattern"

git add src/Conduit.Resilience/RetryPolicy.cs
git commit -m "feat(resilience): Add retry policies"

# 6. Merge to develop
git checkout develop
git merge --no-ff feature/add-resilience-module
```

### Preparing a Release

```bash
# When you have 3-5 new features or monthly schedule

# 1. Create release branch
git checkout -b release/0.2.0 develop

# 2. Bump version
./scripts/bump-version.sh 0.2.0

# 3. Update CHANGELOG.md
# Add release notes for all changes

# 4. Final commit
git add CHANGELOG.md
git commit -m "docs(changelog): Add release notes for 0.2.0"

# 5. Merge to master
git checkout master
git merge --no-ff release/0.2.0
git tag -a v0.2.0 -m "Release 0.2.0"

# 6. Merge back to develop
git checkout develop
git merge --no-ff release/0.2.0

# 7. Push
git push origin master develop --tags
git branch -d release/0.2.0
```

### Emergency Hotfix

```bash
# Critical bug in production

# 1. Branch from master
git checkout -b hotfix/0.1.1 master

# 2. Fix the issue
git commit -m "fix(security)!: Patch critical vulnerability"

# 3. Bump patch version
./scripts/bump-version.sh patch

# 4. Merge to master
git checkout master
git merge --no-ff hotfix/0.1.1
git tag -a v0.1.1 -m "Hotfix 0.1.1: Security patch"

# 5. Merge to develop
git checkout develop
git merge --no-ff hotfix/0.1.1

# 6. Push immediately
git push origin master develop --tags
```

## 🔤 Commit Message Format

```
<type>(<scope>): <short description>

[optional detailed description]

[optional footer]
```

### Types

- **feat**: New feature → Bumps MINOR version
- **fix**: Bug fix → Bumps PATCH version
- **docs**: Documentation → No version bump
- **style**: Formatting → No version bump
- **refactor**: Code restructure → No version bump
- **test**: Tests → No version bump
- **chore**: Maintenance → No version bump

### Examples

```bash
# Simple feature
git commit -m "feat(api): Add ICommand interface"

# With body
git commit -m "feat(messaging): Add priority queue support

Allow messages to be prioritized for processing order.
High priority messages are processed before normal ones.

Closes #123"

# Breaking change
git commit -m "feat(core)!: Simplify component lifecycle

BREAKING CHANGE: Reduced states from 13 to 8.
Components must update their state machine logic."
```

## 🌳 Branching Model

```
master (production)
  └─ develop (integration)
      ├─ feature/add-resilience      ← Your work here
      ├─ feature/kafka-transport
      ├─ fix/message-correlation
      └─ ...
  ├─ release/0.2.0                   ← For release prep
  └─ hotfix/0.1.1                    ← Emergency fixes
```

## 📊 Version Bumping Rules

| Commits Since Last Release | Next Version | Command |
|----------------------------|--------------|---------|
| Only `fix`, `perf` commits | 0.1.0 → 0.1.1 | `./scripts/bump-version.sh patch` |
| At least one `feat` | 0.1.0 → 0.2.0 | `./scripts/bump-version.sh minor` |
| `feat!` with breaking change (0.x) | 0.1.0 → 0.2.0 | `./scripts/bump-version.sh minor` |
| Ready for production | 0.9.0 → 1.0.0 | `./scripts/bump-version.sh 1.0.0` |
| `feat!` after 1.0.0 | 1.5.0 → 2.0.0 | `./scripts/bump-version.sh major` |

## 🛠️ Development Tools

```bash
# Check version
cat VERSION

# Bump version
./scripts/bump-version.sh [major|minor|patch|X.Y.Z]

# View commit history
git log --oneline --graph -10

# See commits since last release
git log v0.1.0..HEAD --oneline

# Check for uncommitted changes
git status

# Build project
dotnet build

# Run tests
dotnet test
```

## 📝 Next Steps

1. **Read the cheat sheet**: `cat .github/CHEATSHEET.md`
2. **Pick a task**: Check `TASK.md` for next modules to implement
3. **Create feature branch**: `git checkout -b feature/name develop`
4. **Code with atomic commits**: One logical change per commit
5. **Follow conventional commits**: Use the format above
6. **Merge to develop**: When feature is complete

## 🎓 Learning Resources

- [Conventional Commits](https://www.conventionalcommits.org/) - Commit format
- [Semantic Versioning](https://semver.org/) - Version numbering
- [Git Flow](https://nvie.com/posts/a-successful-git-branching-model/) - Branching strategy
- [Keep a Changelog](https://keepachangelog.com/) - Changelog format

## ❓ FAQs

**Q: When should I bump the version?**  
A: After accumulating changes (features or fixes), before creating a release branch.

**Q: Can I commit directly to develop?**  
A: For small fixes, yes. For features, use feature branches.

**Q: What if I forgot to follow conventional commits?**  
A: Use `git commit --amend` to fix the last commit, or be better next time!

**Q: When do we release 1.0.0?**  
A: When all 23 modules are complete, API is stable, and production-ready.

**Q: Do I need to update CHANGELOG.md for every commit?**  
A: No, update it when preparing a release (on the release branch).

---

**Happy coding! 🎉**

For questions, check the detailed docs in `.github/` directory.
