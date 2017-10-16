# Contribution guide

## Worflow
1. Create new issue and related branch
2. Make desired changes
3. Push and make pull request
4. The pull PR will leads to review
5. Upon review validating, the PR will be merge in master

## Software versions
* Unity: 2017.1.1f1

## Code convention
* Indents: 4 Spaces, no tabs
* Name in CamelCase
* Const in capital
* Enum values in CAPITAL

## Test
Try to test public method. Unity is using NUnit.

## Git cheatsheet
* `git clone`: Clone a repository
* `git pull`: Get latest change on current branch
* `git add .`: Add all change to index
* `git add <files>`: Add <files> to index
* `git commit`: Add index to commit
* `git push`: Publish commit to the world
* `git checkout <branch>`: Switch to <branch>. Create branch if it doesn't exist. 