# Git Branching Guide for RTChess Team

## Quick Reference

### Starting New Work
```bash
git checkout master
git pull origin master
git checkout -b feature/your-feature-name
```

### Daily Workflow
```bash
# Work on your feature
git add <files>
git commit -m "Clear description of change"
git push origin feature/your-feature-name
```

### Submitting Your Work
1. Push your branch: `git push origin feature/your-feature-name`
2. Create Pull Request on GitHub
3. Wait for review and merge by main account

### Updating Your Branch
```bash
# If master has new changes while you're working
git checkout feature/your-feature-name
git fetch origin
git merge origin/master
# Or use rebase: git rebase origin/master
```

## Branch Naming
- `feature/enemy-ai` - New features
- `fix/unit-rotation` - Bug fixes
- `improve/particles` - Enhancements

## Before You Start Working
**Check team chat**: "I'm working on [filename] today" to avoid conflicts

## Rules
1. ✅ Never push directly to `master`
2. ✅ Always create a feature branch
3. ✅ Pull latest master before starting work
4. ✅ Commit small and often
5. ✅ Write clear commit messages
6. ❌ Don't force push (`--force`) unless you know what you're doing
7. ❌ Don't edit Unity scene files if someone else is working on them

## File Coordination
**Avoid conflicts by coordinating:**
- Scenes: Only one person edits at a time
- Core systems: Coordinate with main account (jai)
- Your feature: You own those files

## Help
- Merge conflict? Ask in team chat
- Unsure about something? Ask before pushing
- Branch stuck? Create a new branch from latest master

---
**Main Account:** jai (this computer)
**Merges To Master:** Via Pull Request review
