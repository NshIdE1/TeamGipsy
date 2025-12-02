# TeamGipsy 学习记录与 AI 辅助

## 概览
- Windows 托盘学习应用，目标是推进每日英语学习与复习，并通过 AI 生成包含当日词汇的英文短文和中文译文。
- 核心特性：
  - SM2+ 间隔重复算法管理复习节奏。
  - 统计“学习记录”：今日学习、纯复习数量、周学习、正确率、连续天数等。
  - Deepseek 接入：按“当日目标数量”的单词生成 150-200 词英文短文，并给出中文译文。
  - Markdown 渲染展示，支持部分选择复制与全文复制。
  - 本地 SQLite 数据库 `Resources/inami.db` 持久化学习进度与配置。

## 运行环境
- 操作系统：Windows
- .NET Framework：4.7.2
- 开发工具：Visual Studio 2017+ 或 MSBuild
- 外部服务：Deepseek（需要 API KEY）

## 依赖与引用
- NuGet 包（见 `TeamGipsy.csproj`）：
  - `CommonServiceLocator`, `Dapper`, `Microsoft.Toolkit.Uwp.Notifications`, `MP3Sharp`, `MvvmLight`, `NPOI`, `Prism.Core`, `System.Data.SQLite`, `System.Reactive`, `Markdig`
- 框架引用：`System.Windows.Forms`, `WindowsFormsIntegration`, `System.Web.Extensions` 等

## 构建与运行
- 使用 Visual Studio
  - 打开解决方案 `TeamGipsy.sln`
  - 选择配置（Debug 或 Release）并构建运行
- 使用命令行（PowerShell）
  - 在项目根目录运行：
    - `msbuild .\TeamGipsy.sln /t:Build /p:Configuration=Release`

## 快速开始
- 启动应用后将驻留在系统托盘。
- 右键托盘图标打开菜单，建议先进入“参数设置→AI配置”填入 `接口地址` 与 `API KEY`。
  - 默认接口地址：`https://api.deepseek.com/v1/chat/completions`（可在配置中覆盖）
- 按 `ALT+Q` 触发当日学习，或在菜单点击“开始！”。
- 达成今日目标后，菜单将显示“加深记忆”，点击会生成包含当日词汇的英文短文与中文译文，并弹窗展示。

## 菜单说明
- 开始！：启动当次学习流程
- 参数设置：
  - 单词个数：设置当次目标单词数
  - 英标类型：设置美式/英式发音偏好
  - 自动播放：自动发音切换
  - 自动日志：学习日志开关
  - 重置进度：重置当前词库学习状态
  - AI配置：设置/更新 `AI接口地址` 与 `API KEY`
- 导入单词：导入 Excel（支持英语或自定义模板）
- 英语词汇：切换词库（CET4/CET6/IELTS/TOEFL/考研等）
- 随机测试：随机单词测试模式
- 学习记录：打开统计视图窗口
- 加深记忆：当达成目标后出现，生成英文短文 + 中文译文弹窗
- 快捷说明：显示快捷键说明
- 开机启动：创建启动快捷方式
- 退出：退出程序

## 学习流程
- 入口：
  - 托盘菜单“开始！”或快捷键 `ALT+Q` 触发 `Begin_Click`
  - 入口代码：`View/TeamGipsy.xaml.cs:421`
- 推送逻辑：
  - SM2 模式：`PushWords.RecitationSM2`（线程执行，包含语音播放与答题流程）
  - 随机测试：`PushWords.UnorderWord`
- 语音播放与提示：通过 `Microsoft.Toolkit.Uwp.Notifications` 与 MP3 播放实现。

## 学习记录指标
- 实现位置：`ViewModel/TeamGipsyModel.cs:70-119`
- 指标含义：
  - `TodayCount`：今日学习数量（`dateLastReviewed == 今天` 的单词数）
  - `ReviewCompleted`：今日“纯复习”完成数（今日学习的单词中，`dateFirstReviewed < 今天`）
  - `WeekCount`：本周学习数量（从本周一到今日的 `dateLastReviewed` 计数）
  - `AccuracyRate`：今日正确率（`lastScore >= Parameters.Correct` 的比例）
  - `StreakDays`：连续学习天数（最近连续日期集合包含今天向前数的天数）

## AI 集成与配置
- 客户端：`Model/Ai/DeepseekClient.cs`
  - `GenerateEssayAsync(words)`：根据传入的“当日词汇（去重后取 `Select.WORD_NUMBER` 个）”生成英文短文；提示词会动态标注必须使用的单词个数与美/英式拼写偏好。
  - `TranslateAsync(text)`：将英文短文翻译为简体中文；优先解析 JSON 的 `choices[0].message.content`，失败则回退正则。
- 配置读写：`Model/SqliteControl/Select.cs:111-164,169-180`
  - `LoadGlobalConfig`：加载 `Global` 表，并仅在非空值时覆盖内置默认；若地址为空，会写回默认值到数据库。
  - `UpdateGlobalConfig`：写回当前配置（包括 `aiApiBase` 与 `aiApiKey`）。
- 菜单入口：`View/TeamGipsy.xaml.cs:336-338,402-406`
  - 点击“AI配置”弹出 Toast 输入框，保存后持久化。

## “加深记忆”弹窗
- 生成逻辑：`View/TeamGipsy.xaml.cs:384-399`
  - 收集今日学习单词，去重后取 `Select.WORD_NUMBER` 个，先生成英文，再生成中文译文。
- 展示窗口：`View/DeepenMemoryWindow.xaml` / `View/DeepenMemoryWindow.xaml.cs`
  - 使用 `Markdig` 将 Markdown 转 HTML，承载于 `WindowsFormsHost + WebBrowser`。
  - 字体较大，行距舒适；展示顺序为英文原文 + 分隔线 + 中文译文。
  - 复制：支持选中部分复制（优先复制选中内容），未选中时复制“原文+译文”全文。

## 数据库结构与迁移
- 数据库文件：`Resources/inami.db`
- 表与字段（关键）：
  - `Global`：`currentWordNumber`, `currentBookName`, `autoPlay`, `EngType`, `autoLog`, `aiApiBase`, `aiApiKey`
    - 加载时在缺失字段时自动 `ALTER TABLE` 增加；地址空时回填默认。
  - 词库表：增加字段 `difficulty`, `daysBetweenReviews`, `lastScore`, `dateLastReviewed`, `dateFirstReviewed`
    - 迁移位置：`Model/SqliteControl/Select.cs:279-298`
  - `Count`：记录当前词库目标与进度（`current`, `number`）
- 学习状态写回：`Model/SqliteControl/Select.cs:312-332`
  - 写入 `status` 与复习相关字段，并用 `COALESCE` 初始化 `dateFirstReviewed` 为首次复习时间。

## 快捷键
- `ALT+Q`：开始内置单词学习
- `ALT+~`：英语单词发音
- `ALT+1` 到 `ALT+4`：答题选项

## 目录结构
```
Model/
  Ai/DeepseekClient.cs           # AI 客户端（生成与翻译）
  PushControl/*                  # 学习推送与交互
  SM2plus/*                      # SM2+ 参数与卡片逻辑
  SqliteControl/Select.cs        # 数据访问、配置与统计
View/
  TeamGipsy.xaml(.cs)            # 学习记录视图与托盘菜单
  DeepenMemoryWindow.xaml(.cs)   # “加深记忆”展示窗口
ViewModel/
  TeamGipsyModel.cs              # 学习记录指标计算
Resources/
  inami.db, mute.mp3, 图标资源等
```

## 常见问题
- 提示“AI设置未配置”
  - 请确保 `Global.aiApiBase` 与 `Global.aiApiKey` 非空；可在“参数设置→AI配置”输入。
  - 若地址为空，系统会写入默认地址；密钥仍需手动填入。
- 子窗体 Owner 异常
  - 主窗体在启动时隐藏，故不设置 Owner；弹窗使用 `Topmost` 显示。
- Markdown 字体过小
  - 已调至较大字号（30px），若需要更大或支持缩放，可进一步扩展。
- “学习记录”中复习数量统计不准确
  - 已修正为仅统计 `dateFirstReviewed < 今天` 且 `dateLastReviewed == 今天` 的单词，表示纯复习数量。

## 安全与隐私
- API Key 存储在本地数据库 `Global.aiApiKey` 字段，请勿分享可执行文件与数据库给他人。
- 不在日志或 UI 中输出密钥；如需上报错误请去除敏感信息。

## 开发说明
- 主线程入口与托盘菜单：`View/TeamGipsy.xaml.cs`
- 学习记录视图：`View/TeamGipsy.xaml`
- 指标计算：`ViewModel/TeamGipsyModel.cs`
- 学习推送流程与 Toast 输入：`Model/PushControl/PushWords.cs`
- 数据访问与迁移：`Model/SqliteControl/Select.cs`
- AI 生成与翻译：`Model/Ai/DeepseekClient.cs`

---