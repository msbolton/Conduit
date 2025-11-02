# Contributing to Conduit Framework

## Conventional Commits

This project uses [Conventional Commits](https://www.conventionalcommits.org/) for clear and structured commit messages.

### Commit Message Format

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types

- **feat**: A new feature
- **fix**: A bug fix
- **docs**: Documentation only changes
- **style**: Changes that don't affect code meaning (formatting, whitespace)
- **refactor**: Code change that neither fixes a bug nor adds a feature
- **perf**: Performance improvements
- **test**: Adding missing tests or correcting existing tests
- **build**: Changes to build system or dependencies
- **ci**: Changes to CI configuration files and scripts
- **chore**: Other changes that don't modify src or test files
- **revert**: Reverts a previous commit

### Scopes

Common scopes in this project:
- `api` - Conduit.Api module
- `common` - Conduit.Common module
- `core` - Conduit.Core module
- `pipeline` - Conduit.Pipeline module
- `messaging` - Conduit.Messaging module
- `components` - Conduit.Components module
- `serialization` - Conduit.Serialization module
- `security` - Conduit.Security module
- `resilience` - Conduit.Resilience module
- `transports` - Transport modules

### Examples

```bash
# Feature addition
feat(api): Add ICommand interface for CQRS pattern

# Bug fix
fix(messaging): Correct message correlation ID tracking

# Documentation
docs: Update README with installation instructions

# Breaking change
feat(core)!: Change component lifecycle state machine

BREAKING CHANGE: The lifecycle now has 13 states instead of 10.
Components must update their state transition logic.

# Multiple commits for atomic changes
git add src/Conduit.Api/ICommand.cs
git commit -m "feat(api): Add ICommand interface"

git add src/Conduit.Api/ICommandHandler.cs
git commit -m "feat(api): Add ICommandHandler interface"
```

### Atomic Commits

- **One logical change per commit**: Each commit should represent a single, complete change
- **All tests should pass**: Never commit broken code
- **Keep commits small**: Easier to review and revert if needed
- **Write descriptive messages**: Explain the "why" not just the "what"

### Commit Template

Use the provided `.gitmessage` template:

```bash
git config commit.template .gitmessage
```

When you commit, the template will guide you through the format.

### Tools

Consider using [commitlint](https://commitlint.js.org/) to validate commits:

```bash
npm install --save-dev @commitlint/cli @commitlint/config-conventional
```

## Pull Request Guidelines

1. Create atomic commits following conventional commits
2. Ensure all tests pass
3. Update documentation as needed
4. Reference related issues in PR description
5. Keep PRs focused on a single feature or fix

## Branching Strategy

- `master` - Production releases
- `develop` - Development branch
- `feature/*` - Feature branches
- `fix/*` - Bug fix branches
- `release/*` - Release preparation branches

### Creating a Feature Branch

```bash
git checkout develop
git pull origin develop
git checkout -b feature/add-kafka-transport
```

### Merging

```bash
git checkout develop
git merge --no-ff feature/add-kafka-transport
git push origin develop
```

## Code Style

- Follow C# naming conventions
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Add XML documentation for public APIs
- Keep warnings as errors enabled
- Run code analysis before committing

## Testing

- Write unit tests for new features
- Maintain >80% code coverage
- Use xUnit for testing framework
- Mock external dependencies

Thank you for contributing to Conduit! 🎉
