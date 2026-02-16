# DKNet Framework - GitHub Copilot Skills

This folder contains GitHub Copilot Agent Skills designed to help developers use DKNet Framework NuGet packages effectively in their applications.

## 📚 What Are Copilot Skills?

Copilot Skills are structured guidance documents that enhance GitHub Copilot's ability to generate code using specific frameworks and libraries. Each skill provides:

- **Pattern recognition** - How to identify when to use specific patterns
- **Code templates** - Pre-built patterns and examples
- **Best practices** - Framework-specific conventions and standards
- **Common pitfalls** - What to avoid and why
- **Quick references** - Fast access to common operations

## 🎯 Available Skills

### Core Skills

- **[dknet-overview](dknet-overview/SKILL.md)** - Framework overview, architecture, and getting started guide
- **[fw-extensions](fw-extensions/SKILL.md)** - Core framework extensions and utilities

### EF Core Skills

- **[efcore-specifications](efcore-specifications/SKILL.md)** - Specification pattern with dynamic predicates
- **[efcore-repos](efcore-repos/SKILL.md)** - Repository pattern implementation
- **[efcore-abstractions](efcore-abstractions/SKILL.md)** - Base entities and common abstractions

### ASP.NET Core Skills

- **[aspcore-idempotency](aspcore-idempotency/SKILL.md)** - Idempotent operations in APIs

### Messaging Skills

- **[slimbus-messaging](slimbus-messaging/SKILL.md)** - Message bus patterns and handlers

## 🚀 How to Use These Skills

### Option 1: Repository-Level (Recommended for DKNet Development)

Skills in this folder are automatically available to GitHub Copilot when working in this repository.

### Option 2: Global Installation (For Applications Using DKNet)

To use these skills across all your projects that consume DKNet NuGet packages:

1. **Copy the Skills folder** to your global Copilot skills directory:
   ```bash
   # For GitHub Copilot
   mkdir -p ~/.copilot/skills
   cp -r Skills/* ~/.copilot/skills/
   
   # For Claude Code (if using)
   mkdir -p ~/.claude/skills
   cp -r Skills/* ~/.claude/skills/
   ```

2. **Or symlink for automatic updates**:
   ```bash
   ln -s /path/to/DKNet/Skills ~/.copilot/skills/dknet
   ```

3. **Verify installation**:
   - Open VS Code with GitHub Copilot
   - Type a comment like `// Create a specification for active users`
   - Copilot should use DKNet patterns automatically

### Option 3: Per-Project Basis

Copy specific skills to your project's `.github/skills/` folder:

```bash
mkdir -p .github/skills
cp -r /path/to/DKNet/Skills/efcore-specifications .github/skills/
```

## 📖 Skill Structure

Each skill follows the standard GitHub Copilot Agent Skill format:

```
skill-name/
├── SKILL.md              # Main skill document with YAML frontmatter
├── examples/             # Optional: Code examples
│   ├── basic.cs
│   └── advanced.cs
└── resources/            # Optional: Additional resources
    └── patterns.md
```

## 🎓 Learning Path

**New to DKNet Framework?** Follow this path:

1. Start with **[dknet-overview](dknet-overview/SKILL.md)** for architecture understanding
2. Learn **[fw-extensions](fw-extensions/SKILL.md)** for core utilities
3. Explore EF Core skills based on your needs:
   - **[efcore-abstractions](efcore-abstractions/SKILL.md)** - Start here for entities
   - **[efcore-specifications](efcore-specifications/SKILL.md)** - Query building
   - **[efcore-repos](efcore-repos/SKILL.md)** - Data access layer
4. Add **[aspcore-idempotency](aspcore-idempotency/SKILL.md)** for API development
5. Use **[slimbus-messaging](slimbus-messaging/SKILL.md)** for event-driven architecture

## 🔍 Quick Reference

### When to Use Each Skill

| Task | Skill to Use |
|------|--------------|
| Creating entities | `efcore-abstractions` |
| Building queries | `efcore-specifications` |
| Implementing repositories | `efcore-repos` |
| Making APIs idempotent | `aspcore-idempotency` |
| Using utility extensions | `fw-extensions` |
| Publishing/subscribing events | `slimbus-messaging` |
| Understanding architecture | `dknet-overview` |

### Common Copilot Prompts

```csharp
// Create a specification for active products with a category filter
// (Uses efcore-specifications skill)

// Implement a repository for the User entity
// (Uses efcore-repos skill)

// Add idempotency to this POST endpoint
// (Uses aspcore-idempotency skill)

// Create a message handler for OrderCreated events
// (Uses slimbus-messaging skill)
```

## 📦 Framework Versions

These skills are designed for:
- **.NET**: 9.0+
- **C#**: 13+
- **EF Core**: 9.0+
- **ASP.NET Core**: 9.0+

## 🤝 Contributing

To improve or add skills:

1. Follow the [SKILL.md template](SKILL_TEMPLATE.md)
2. Include real examples from the framework
3. Test with GitHub Copilot to ensure effectiveness
4. Submit a PR with your changes

## 📄 License

MIT License - See [LICENSE](../LICENSE) for details

## 🔗 Additional Resources

- [DKNet Framework Documentation](../docs/README.md)
- [Memory Bank](../src/memory-bank/README.md) - Detailed development guides
- [GitHub Copilot Skills Documentation](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills)
- [NuGet Packages](https://www.nuget.org/packages?q=DKNet)

---

**Version**: 1.0.0  
**Last Updated**: February 2026  
**Status**: Production Ready
