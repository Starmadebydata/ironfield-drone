# Ironfield — 从原型到可交付产品的路线图

现状(2026-09-11):单场景 `Mission01`,IMGUI HUD,一种无人机、三种载具、固定
车队,无主菜单/存档/设置,音频是占位合成音,Built-in 渲染管线未做美术终审。
可玩、可通关/失败,但只是一条**可玩的竖切片**,不是产品。

下面按"能不能发布"划分四个阶段。P0 是把当前这一关做扎实到能放到 itch.io 上
让陌生人玩不懵、不出戏;P1 让它像个游戏而不是一关;P2/P3 是打磨与上架。

---

## P0 — 补齐"游戏"最基本的骨架(必须,约 1-2 周量级)

没有这些,现在连"一个游戏"都算不上,只是一个能跑的场景。

**状态(2026-09-11):1、2、3、4、6 已实现;5 做了范围调整,见下。**

1. ✅ **主菜单场景** `MainMenu.unity`:开始 / 设置 / 退出。`Ironfield.Core.SceneFlow`
   负责场景切换(`LoadMainMenu` / `LoadMission` / `RestartCurrent` / `Quit`),
   替代了之前"编辑器里直接开 Mission01 按 Play"的方式。构建的 Build Settings
   现在是 `MainMenu` → `Mission01`,打包出的游戏会先进主菜单。
2. ✅ **暂停菜单**(`PauseMenu.cs`):Esc 呼出,继续/操作说明/设置/重开/回
   主菜单,`Time.timeScale` 和鼠标锁定(`HudController` 检查
   `PauseMenu.IsPaused`)都接好了。
3. ✅ **设置菜单**(`SettingsGUI.cs` + `GameSettings.cs`):主/音效/引擎音量、
   鼠标灵敏度(接入 `DroneController`)、Y轴反转、画质预设,`PlayerPrefs` 持久化。
   引擎音量已经在接到 `MotorPitch`;音效音量的滑条先留着——游戏里目前还没有
   真正的 SFX 音源可接(P1 加真实音效时接上)。
4. ✅ **结果流程收尾**:结束面板(HudController.DrawEndPanel)加了 "Main Menu"
   按钮,和已有的 Restart/Quit 一起走 `SceneFlow`。
5. ⚠️ **范围调整,IMGUI → uGUI + TextMeshPro 未做**:新写的主菜单/暂停/设置/
   首次引导全部用 IMGUI(跟现有 HUD 一致),没有换成 uGUI+TMP。原因:这次是
   无头(headless batchmode)迭代,没有交互式编辑器在场做可视化搭 UI 和调
   TextMeshPro 的 Essential Resources 导入——那一步历史上就是手动/交互式流程,
   之前 glTFast 包在这个沙箱里因为网络受限直接失败过(见 CREDITS.md 相关提交),
   TMP 虽是内置包不用联网,但资源导入路径没有验证过,贸然引入会把风险重新
   引回本该稳的 P0。IMGUI 这条路已经在结束面板上验证好用(按钮可点、无障碍)。
   **保留为 P2 任务**:等你有空在编辑器里过一遍界面观感的时候,再把这几个
   IMGUI 面板换成 uGUI+TMP 或做视觉设计,那时候可以顺手定好美术风格。
6. ✅ **首次运行引导**(`FirstRunTip.cs`):第一次进任务前强制一屏操作提示,
   `PlayerPrefs` 记一次性标记,暂停菜单里"操作说明"可以随时重看。
   注意:没有用 `Time.timeScale=0` 冻结(第一版这样做过,发现会把 PlayMode
   自动化测试里基于 `Time.time` 的等待循环挂死——测试环境里从没设置过"已读"
   标记,所以每次都会显示这个提示)。改成只禁用无人机输入,不冻结时间。

验收标准:一个没读过 README 的人,双击构建出的 .app,能自己摸到"开始
游戏→设置灵敏度→打完一关→看到结果→回菜单"整条链路,不需要你在场解释。
**这条链路本身已经打通并跑通了自动化测试**(EditMode 9/9、PlayMode 3/3,新增
一条 `MainMenu_loads_without_errors` 冒烟测试);还没做过的是真人手动过一遍
交互手感(按钮大小/点击区域/文字排版),建议你在编辑器里打开 `MainMenu.unity`
亲自点一遍。

## P1 — 让它是"一款游戏"而不是"一关"(核心内容量)

**状态(2026-09-11):1、2、3 已实现;4 明确砍掉留到之后;5 做了范围调整。**

1. ✅ **多任务/关卡**:3 关,难度递增(车队规模 6→7→8、有无防空火力点
   0→1→2、车队速度 1.0→1.15→1.3 倍)。用编辑器时(非运行时)的
   `MissionBuildConfig`(`IronfieldSetup.cs`)驱动 `BuildScene`,而不是运行时
   ScriptableObject——场景本来就是无头预烘焙的,运行时不需要再读一份数据。
   **范围调整**:没做"地形/天气/时段"随关卡变化,3 关地形/村庄是同一份(同
   一套固定噪声种子),只有车队构成和敌方部署不同。加地形差异化需要给
   `BuildTerrain`/`ScatterVegetation`/`ScatterRuins` 加种子偏移参数,单独算一
   块工作量,先留到后面。
2. ✅ **关卡选择/进度**:`Ironfield.Core.CampaignProgress`(PlayerPrefs)记录
   已解锁关卡数 + 每关最佳分数/评级;`Ironfield.Core.MissionCatalog` 是固定
   编译期关卡表(id/显示名/场景名)。主菜单"开始任务"现在是关卡列表,锁定的
   关卡显示 🔒,已打过的显示最佳评级。赢关面板加了"Next Mission ▶"直接进
   下一关。
3. ✅ **敌方与目标多样性**:
   - 静态防空点(`BuildFlakPosition`):独立的 `VehicleTurret`(不挂
     `Vehicle`/`HealthComponent`,打不掉——是要绕开的地形,不是击杀目标),
     射速比车载炮慢、散布更宽,视觉上是杆+炮管+沙包,跟车辆炮塔一看就有区别。
   - 高价值目标(`Vehicle.highValue`):Mission03 车队最后一辆车,红旗+红色
     信标点亮清晰可见,击毁额外 +750 分(`MissionManager` 订阅
     `VehicleRegistry.AnyDestroyed` 统计)。
   - 车队受击后分散/加速反应:**发现这个其实已经实现了**
     (`VehicleConvoyAI.ReactToAllyLost`,同伴被摧毁后短暂停顿再以 1.7 倍速
     通过),这次只是让它在更难的关卡里更常触发。
4. ⛔ **无人机进阶(第二种无人机):这次没做**。原因是范围——新机型需要新
   prefab + `DroneTuning` 变体 + 选择/解锁 UI,单独是一块不小的工作量,做了
   容易把这次改动拖成一个既不稳又难 review 的大 diff。建议单独作为下一个
   任务来做,现在多任务系统的骨架(`MissionCatalog`/`CampaignProgress`)已经
   稳定,加"选无人机"UI 有地方接了。
5. ✅ **音频**(2026-09-11 补):自己上 kenney.nl 找到了直链(Kenney 的下载
   按钮背后有个隐藏的 `#inline-download` 弹层,里面才是真实 zip 地址,不是
   靠猜——跟 freesound.org 需要 OAuth 不同,Kenney 全部 CC0 且直链可
   `curl`)。接入了三个真实录音,换掉了对应的合成占位音:爆炸/载具摧毁用
   Sci-fi Sounds 包的 `lowFrequency_explosion`,命中提示音用 Impact Sounds
   包的 `impactMetal_medium`,新加的 UI 点击反馈音用 Interface Sounds 包的
   `click`(`Assets/Scripts/UI/UiSfx.cs`,`UiSfx.Button` 包了一层
   `GUI.Button`,菜单/暂停/设置/结束面板的按钮全部换过去了)。素材存
   `Assets/Audio/External/`,原始文件存 `tools/external_src/audio/`,署名在
   CREDITS.md,流程跟模型资产完全一致。`GameSettings.SfxVolume` 现在真的有
   声音源在听。**无人机引擎循环仍是合成音**——真实录音做循环需要专门处理
   接缝,风险和收益都跟一次性音效不是一回事,优先级更低,留着。

验收标准:一个玩家愿意打完 3 关而不是打完 1 关就关掉。**3 关已经可打通**,
带 `MainMenu_loads_without_errors` 之外的两条新 PlayMode 冒烟测试验证
Mission02/03 的防空点数量和高价值目标存在;`CampaignProgress` 有 5 条
EditMode 单测(解锁链、最佳成绩、胜负不同结果)。EditMode 14/14、PlayMode
5/5。

## P2 — 打磨与工程债(让它经得起别人看)

**状态(2026-09-11):2、3(一半)、4(三分之二)、5 已实现;1 明确留到有交互式
编辑器会话时再做。**(最初把"真机"理解成了需要专门的目标测试设备,用户
指出这其实就是"把游戏编译成正式安装包,在会用来玩的电脑上跑"——这台 Mac
本身就是目标机器,不需要额外硬件,于是把 2 补上了。)

1. ⛔ **URP 迁移:这次没做**。这本来就是路线图里标了"建议单独开分支做"的
   一次性大改动——材质升级向导、自定义 `IronfieldPost.shader` 在 URP 下的
   兼容性、光照参数重调,都需要边改边在编辑器里肉眼核对画面,而这次是纯
   无头(headless batchmode)执行,没有交互式会话能做这种视觉验收。贸然做会
   把"看起来对不对"这件事变成完全靠猜。继续按原计划留到有空开编辑器窗口
   核对画面的时候单独做。
2. ✅ **真机性能 profiling**(2026-09-11 补):加了 `PerfHarness.cs`——用
   `-perftest` 命令行参数启动正式构建的版本,会自动跳过菜单直接进最重的
   `Mission03`,强制无人机全速直飞 20 秒,记录帧率后自动退出并写日志。配套
   `IronfieldSetup.BuildDevPlayer()`(Ironfield 菜单第 9 项)一键出 macOS
   Development Build。**实测结果**(这台 Mac,1280×720,Ultra 画质,关闭
   vsync 跑满帧):平均 **133~176 fps**,余量很大;开 vsync(正常玩家体验)
   稳定贴着屏幕刷新率跑,不掉帧。发现一个真实问题:任务刚开始的头几秒有
   一次性的严重卡顿(单帧掉到 0.5 fps,即那一帧卡了几乎 2 秒),延长测量前
   的预热时间后这次卡顿消失——判断是**首次出现的着色器变体现场编译**导致
   (村庄/防空点/信标灯这些东西第一次进入视野时触发),不是持续性性能问题,
   典型 Unity 游戏常见的"首次加载卡顿",修复方向是 Shader Variant
   Collection 预热,留作后续单独任务。另外预热后仍有零星帧掉到 15~17 fps
   (1% low),量级不影响游戏体验,但没来得及用真正的 Unity Profiler(需要
   交互式编辑器连接)定位是哪个系统——车队 AI 每帧的地面吸附射线检测、炮塔
   视线检测是比较合理的嫌疑对象,值得后续用 Profiler 实测确认。
   **LOD 没做**:现在的余量(133+ fps)还远不到需要 LOD 的地步,做 LOD 目前
   仍是没有必要的复杂度。
3. ⚠️ **输入完善,做了一半**:
   - ✅ **输入设备自动切换提示**:`DroneInput` 现在跟踪最近一次有效输入来自
     鼠标键盘还是手柄(`DroneInput.LastWasGamepad`),暂停菜单"操作说明"、
     任务内右下角操作提示、开局的出击须知,三处文案会跟着切换成对应的
     按键名(鼠标/WASD vs 右摇杆/ABXY)。
   - ⛔ **按键重绑定 UI 没做**:手柄/键鼠这套输入是在 `DroneInput.Read()`
     里直接轮询 `Keyboard.current`/`Gamepad.current` 的硬编码按键,没有
     `.inputactions` 资产可绑定——真正支持重绑定需要先把整套输入迁到
     Input Actions 架构,这是一次基础性重构,不是"顺手加个 UI"能带过的,
     留到之后单独做。
4. ⚠️ **可访问性,做了两项半**:
   - ✅ 色盲友好目标标记颜色:设置里"色盲模式"开关,把目标框/锁定环/命中
     闪光从红/黄换成蓝/橙(对红绿色盲更安全的经典配色对),默认关闭。
   - ✅ 可关闭镜头震动:设置里"镜头震动"开关,关掉后 `DroneCameraRig.Shake`
     直接不生效。
   - ⛔ **UI 缩放选项没做**:游戏里好几个独立 MonoBehaviour 各自有自己的
     `OnGUI`(菜单/暂停/HUD/首次引导),`GUIUtility.ScaleAroundPivot` 修改的
     是全局 `GUI.matrix`,任何一处提前 `return`(现有代码里到处都是)就会让
     缩放矩阵漏到下一个组件的 `OnGUI` 里,污染画面。要安全做这件事得先把
     UI 收敛到一个根组件,或者干脆等 uGUI 迁移(P2 遗留任务)顺带解决。
5. ✅ **稳定性**:新增 `StabilityTests.cs`(3 条 PlayMode)专门补"切场景/
   暂停不留悬挂状态"这类此前真实踩过的坑——第一条直接回归测试了
   `FirstRunTip` 那次冻结 `Time.timeScale` 搞挂自动化测试的问题类型(断言
   任务场景一加载 `Time.timeScale` 必须是 1、`PauseMenu.IsPaused` 必须是
   false);第二条断言连续三次重开任务场景不会残留重复的
   `MissionManager`/`HudController`/`PauseMenu`;第三条专门测
   `UiSfx`(DontDestroyOnLoad 单例)在"主菜单→任务→主菜单"这种真实会发生
   的来回切换下不会产生重复实例。EditMode 14/14、PlayMode 8/8。

## P3 — 上架准备(如果目标是真的发布)

1. **构建与分发**:选平台(建议先 itch.io,免费/免审核,验证有没有人玩);
   Mac + Windows 各出一个 Development→Release 构建脚本
   (`tools/build_release.sh` 之类,复用现有 headless 套路)。
2. **商店素材**:图标、3-5 张截图、30 秒预告(现有 smoke screenshot 管线
   可以直接扩展成"自动录屏脚本")、商店描述文案。
3. **法务**:分发构建前逐条核对 `CREDITS.md` 里的 CC-BY 模型署名是否随
   构建物一起可见(比如加进 `MainMenu` 的"制作人员"页,而不是只存在于仓库)。
4. **反馈渠道**:itch.io 评论区 / 一个简单的 issue 模板,收集真实玩家的
   上手卡点。

---

## 建议的执行顺序

**P0 全部 → P1 的第 1-3 条(多关卡+进度+敌方多样性)→ 再决定要不要做 P2/P3。**
理由:P0 没做之前,给任何人玩都要你在场手把手操作,这比"美术不够精细"是
严重得多的产品缺陷,优先级必须最高。P1 前两条比更多美术/音频更影响"这是不是
一个游戏"的观感。P2/P3 只有在确认要真发布时才值得投入。

## 不建议现在做的事

- 多人/联机、复杂天赋系统、叙事过场:这类项目的常见范围蔓延陷阱,原型阶段
  引入只会拖慢 P0/P1。
- 移动端适配:触屏下"鼠标瞄准飞行"这套核心操作方案需要重新设计,不是简单
  改分辨率的事,等 PC 版验证好玩再谈。
