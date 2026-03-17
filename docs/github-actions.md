# GitHub Actions ワークフロー ドキュメント

このドキュメントでは、`.github/workflows` に定義されている GitHub Actions ワークフローの設定について説明します。

---

## ワークフロー一覧

| ファイル名 | ワークフロー名 | 概要 |
|---|---|---|
| `claude.yml` | Claude Code | `@claude` メンションに応答してタスクを実行する |
| `claude-code-review.yml` | Claude Code Review | プルリクエストの自動コードレビューを行う |
| `issue-triage.yml` | Claude Issue Triage | Issue 作成時に自動でラベル付けを行う |

---

## `claude.yml` — Claude Code

### 概要

Issue コメント、PR コメント、PR レビュー、Issue の作成・アサインなどのイベントで `@claude` とメンションされたとき、Claude が自動的にタスクを実行します。

### トリガー

| イベント | 条件 |
|---|---|
| `issue_comment` (created) | コメント本文に `@claude` が含まれる場合 |
| `pull_request_review_comment` (created) | コメント本文に `@claude` が含まれる場合 |
| `pull_request_review` (submitted) | レビュー本文に `@claude` が含まれる場合 |
| `issues` (opened, assigned) | Issue 本文またはタイトルに `@claude` が含まれる場合 |

### 必要な権限

| 権限 | レベル |
|---|---|
| `contents` | `read` |
| `pull-requests` | `read` |
| `issues` | `read` |
| `id-token` | `write` |
| `actions` | `read` (CI 結果の読み取りに必要) |

### 必要なシークレット

| シークレット名 | 用途 |
|---|---|
| `CLAUDE_CODE_OAUTH_TOKEN` | Claude Code の認証トークン |

### カスタマイズ

`claude.yml` では以下のオプションが利用可能です（コメントアウトで無効化されています）。

```yaml
# カスタムプロンプトの指定（省略時は @claude メンションの指示が使われる）
prompt: 'プルリクエストの説明を更新してください。'

# 使用ツールの制限など追加オプション
claude_args: '--allowed-tools Bash(gh pr:*)'
```

---

## `claude-code-review.yml` — Claude Code Review

### 概要

プルリクエストが作成・更新されたとき、Claude が自動的にコードレビューを実施します。

### トリガー

| イベント | アクション |
|---|---|
| `pull_request` | `opened`, `synchronize`, `ready_for_review`, `reopened` |

特定のファイルパスに限定してトリガーすることも可能です（デフォルトはすべての変更）:

```yaml
# paths:
#   - "src/**/*.ts"
#   - "src/**/*.tsx"
```

### 必要な権限

| 権限 | レベル |
|---|---|
| `contents` | `read` |
| `pull-requests` | `read` |
| `issues` | `read` |
| `id-token` | `write` |

### 必要なシークレット

| シークレット名 | 用途 |
|---|---|
| `CLAUDE_CODE_OAUTH_TOKEN` | Claude Code の認証トークン |

### カスタマイズ

PR 作成者によってレビューの実行を絞り込むことができます（デフォルトは全 PR が対象）:

```yaml
# if: |
#   github.event.pull_request.user.login == 'external-contributor' ||
#   github.event.pull_request.author_association == 'FIRST_TIME_CONTRIBUTOR'
```

---

## `issue-triage.yml` — Claude Issue Triage

### 概要

Issue が新規作成されたとき、Claude が自動的に内容を解析し、適切なラベルを付与します。

### トリガー

| イベント | アクション |
|---|---|
| `issues` | `opened` |

### 必要な権限

| 権限 | レベル |
|---|---|
| `contents` | `read` |
| `issues` | `write` |

### 必要なシークレット

| シークレット名 | 用途 |
|---|---|
| `CLAUDE_CODE_OAUTH_TOKEN` | Claude Code の認証トークン |
| `GITHUB_TOKEN` | Issue へのラベル付けに使用する GitHub トークン |

### 必要なファイル

このワークフローは以下のファイルが存在することを前提としています。

| ファイル | 用途 |
|---|---|
| `.claude/commands/label-issue.md` | ラベル付けのカスタムプロンプト定義 |
| `scripts/edit-issue-labels.sh` | Issue にラベルを適用するスクリプト |

### カスタマイズ

`allowed_non_write_users: "*"` を設定することで、リポジトリへの書き込み権限を持たないユーザーが作成した Issue もトリガー対象になります。

```yaml
allowed_non_write_users: "*"
```

特定ユーザーのみに制限する場合は、この設定を変更してください。

---

## セットアップ

各ワークフローを使用するには、リポジトリに以下のシークレットを設定してください。

1. GitHub リポジトリの **Settings → Secrets and variables → Actions** を開く
2. **New repository secret** をクリック
3. `CLAUDE_CODE_OAUTH_TOKEN` という名前で Claude Code の OAuth トークンを登録する

詳細は [claude-code-action の公式ドキュメント](https://github.com/anthropics/claude-code-action/blob/main/docs/usage.md) を参照してください。
