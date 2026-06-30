# Jira 知识库生成器

从 Jira 问题自动生成知识库文档。支持按模块分类、拆分文件、创建索引。

## 输入参数

- `jql`: Jira 查询条件（JQL 格式）
- `workdir`: 工作目录路径（可选，默认为当前 `.workdir` 设置）

## 执行流程

### Phase 1: 搜索问题

```bash
cd "$(cat ~/.claude/.workdir)" && \
PYTHONIOENCODING=utf-8 python ~/.claude/skills/chanjet-jira/scripts/devops_cli.py jira search "<jql>" 200
```

保存结果到 `issues_raw.json`

### Phase 2: 提取 Issue Keys

从搜索结果中提取所有 issue keys，保存到 `issue_keys.txt`

### Phase 3: 批量获取详情

逐个获取问题的完整字段：
- `summary`: 标题
- `description`: 描述
- `components`: 模块
- `customfield_12007`: 方案/脚本说明

过滤掉方案为空的问题，保存到 `issues_with_solution.json`

### Phase 4: 模块映射

读取 `module_mapping.json`，将 components 字段映射到目标目录：
- 新模块：创建新目录
- 已有映射：合并到现有目录
- 未映射模块：记录后询问用户

### Phase 5: 生成知识文档

每个问题单独生成一个 `.md` 文件：
- **文件命名**：用问题主题命名，移除问题编号 `【Tplus-XXX】`
- **文件结构**：
  ```
  # 问题主题
  
  ## 关键词
  xxx, xxx, xxx
  
  **问题编号**: SERVICE-XXXX
  
  ## 问题现象
  ...
  
  ## 原因分析
  ...
  
  ## 处理步骤
  ...
  
  ## 相关Jira问题
  - SERVICE-XXXX
  ```

### Phase 6: 创建索引

为每个模块目录创建 `index.json`：
```json
{
  "version": "1.0",
  "documents": [
    {
      "file": "问题主题.md",
      "summary": "问题主题"
    }
  ]
}
```

### Phase 7: 更新映射文件

将新发现的模块添加到 `module_mapping.json`

## 关键代码

### 文件名清理

```python
def sanitize_filename(title):
    # 移除【Tplus-XXX】格式编号
    title = re.sub(r'【[^】]+】', '', title).strip()
    # 移除特殊字符
    title = re.sub(r'[\\/:*?"<>|]', '', title)
    # 截断过长标题
    if len(title) > 50:
        title = title[:50]
    return title if title else '未命名问题'
```

### 方案解析

```python
def parse_solution(solution_text):
    result = {'现象': '', '原因': '', '方案': ''}
    # 解析 "问题现象"、"问题原因"、"问题方案" 章节
    ...
```

### 关键词提取

```python
def extract_keywords(summary, solution):
    key_terms = ['消息', '审批', '补丁', '权限', ...]
    # 从标题和方案中匹配关键词
    ...
```

## 注意事项

1. **编码处理**：Windows 环境需设置 `PYTHONIOENCODING=utf-8`
2. **批量获取**：200+ 问题需逐个获取详情，耗时约 10 分钟
3. **模块确认**：未映射模块需用户确认目标目录
4. **文件命名**：避免特殊字符，最长 50 字符

## 输出物

| 文件 | 说明 |
|------|------|
| `{模块}/xxx.md` | 知识文档，每个问题一个 |
| `{模块}/index.json` | 模块索引 |
| `module_mapping.json` | 模块映射表（更新） |
| `issues_with_solution.json` | 原始数据（中间文件） |

## 调用示例

```
/jira-knowledge-gen "project = SERVICE AND status = Closed AND cf[22810] = 是" "D:/tpluscode/tcknowledge"
``