# Ironfield — 从原型到可交付产品的路线图

现状(2026-09-12):三关 `Mission01-03`(2048×2048m 地图,带边界系统+
可选支线目标/地标),IMGUI 主菜单/HUD,两种无人机(轻型/重型,打通
Mission01 解锁重型)、四种载具(坦克/IFV/卡车/自行高炮)、关卡进度存档,
音频接了部分真实 CC0 录音,**已迁移到 URP**(Volume 后处理调色代替了旧的
Built-in 自定义 shader),**支持 7 种语言**(英/法/日/德/西 + 简繁中文,默认
英语)。可玩、可通关/失败,内容量比早期的单关竖切片厚不少,已经在准备
itch.io 上架(P3,进行中)。

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

**状态(2026-09-11):1、2、3、4 已实现;5 做了范围调整。**

1. ✅ **多任务/关卡**:3 关,难度递增(车队规模 6→7→8、有无防空火力点
   0→1→2、车队速度 1.0→1.15→1.3 倍)。用编辑器时(非运行时)的
   `MissionBuildConfig`(`IronfieldSetup.cs`)驱动 `BuildScene`,而不是运行时
   ScriptableObject——场景本来就是无头预烘焙的,运行时不需要再读一份数据。
   **2026-09-12 补上了地形差异化**(见下面"内容密度"一节):`terrainSeedOffset`
   让 3 关地形高度/纹理噪声采样不同区域(不是同一份地形换车队),
   `dryness`/`moodTint`/`vegetationDensityMul` 让后面几关看起来更干燥/更
   荒芜/更战损。仍然没做的是"天气/时段"变化(晴天↔阴天/白天↔黄昏那种),
   目前 3 关光照本身还是同一套。
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
4. ✅ **无人机进阶(第二种无人机,2026-09-11 补)**:重型无人机
   `Drone_Heavy`——体型放大 1.22 倍、橄榄绿装甲色调(`Militarize` 复用坦克
   的装甲染色处理),`DroneTuning_Heavy`:血量 95(轻型 55)、弹头伤害 900/
   半径 6.5(轻型 650/5.5)、最高速 17/30(boost)明显慢于轻型的 22/40、
   偏航率 85(轻型 110)——用血厚火力猛换机动性,不是数值上无脑更强。
   - `GameSettings.SelectedDrone`(PlayerPrefs 持久化)记录玩家选择,
     `MissionManager.SpawnDrone` 按此字段挑 `dronePrefab` 还是
     `dronePrefabHeavy`。
   - 解锁门槛复用现成的 `CampaignProgress.IsUnlocked`(打通 Mission01 解锁,
     跟 Mission02 同一档),没有另起一套解锁系统。
   - 主菜单任务列表上方新增选择器(`MainMenuController.DrawDronePicker`),
     未解锁显示 🔒 和"打通任务01解锁";HUD 的 DRONES 计数旁标注当前机型
     (轻型/重型)。
   - `IronfieldSetup.cs` 把 `BuildDronePrefab` 参数化成吃一个
     `DroneBuildConfig`(名字/缩放/染色),Light/Heavy 两份配置共用同一套构建
     代码,而不是复制一份构建方法。
   - 测试:新增 `DroneSelectionTests.Heavy_drone_has_more_health_than_light`
     (PlayMode)——实际加载 Mission01、按 `SelectedDrone` 验证生成的是哪个
     prefab、并验证重型血量确实更高,不是只测 UI 点没点得通。
   - **范围调整**:没做"选择后影响任务简报文案"之类的联动,选择纯粹是数值
     和外观;也没有做第三种机型或按任务限定可选机型——按用户原话"先做第二种
     无人机",先把两选一的骨架和一个真正有区分度的对照组做扎实。
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

**状态(2026-09-12):1、2、4 已实现;3 做了一半;5 已实现。**(最初把
"真机"理解成了需要专门的目标测试设备,用户指出这其实就是"把游戏编译成正式
安装包,在会用来玩的电脑上跑"——这台 Mac 本身就是目标机器,不需要额外硬件,
于是把 2 补上了。)

1. ✅ **URP 迁移(2026-09-12 补)**:之前明确"这次没做",理由是材质升级、
   自定义 shader 兼容性、光照重调都"需要边改边在编辑器里肉眼核对",而无头
   会话没有交互式验收手段——**这次补上的关键是发现整个项目的材质从来不是手工
   在 Inspector 里配的,而是 `IronfieldSetup.cs` 里 `MakeStandard`/
   `MakeUnlit`/`MakeFx` 这三个函数统一代码生成的**,所以"肉眼核对"这件事
   可以用这次会话里已经反复验证过的手段代替——改代码 → 无头 rebuild → 无头
   截图 → 读图核对,不需要交互式编辑器。
   - `EnsureUrpAsset()`(Ironfield 菜单第 0b 项):脚本创建
     `UniversalRendererData` + `UniversalRenderPipelineAsset`
     (`UniversalRenderPipelineAsset.Create(rendererData)`,公开 API,不是
     反射黑魔法),配置阴影距离/级联/分辨率(URP 把这些配置放在 Pipeline
     Asset 上,不再是 `QualitySettings`),赋给
     `GraphicsSettings.defaultRenderPipeline` 和
     `QualitySettings.renderPipeline`。
   - 材质:`Shader.Find("Standard")` 全部换成
     `"Universal Render Pipeline/Lit"`,`_Glossiness` 属性名改成 URP 的
     `_Smoothness`(`_Metallic`/`.color` 两边同名,不用改——URP shader 上的
     `[MainColor]` 特性让 `Material.color` 照常生效)。FX/粒子材质
     (`MakeFx`,原来用 `Sprites/Default`)换成
     `"Universal Render Pipeline/Unlit"` + 手动挡 Transparent surface 的
     那一套属性/关键字/renderQueue(`SetTransparent` 辅助方法)——等效于
     以前 Inspector 里把 Surface Type 切到 Transparent 那个操作。
   - `Run()` 的素材清理逻辑从"只删 `M_ext_*`"改成"删 `SettingsDir` 下全部
     材质",不然旧材质文件会照样引用 Built-in Standard shader,改了代码也
     不会在磁盘上生效——**副作用是顺手清掉了一批早就没人引用的孤儿材质
     文件**(`M_bark.mat`/`M_leaf0.mat`/`Sky.mat`/没加 `fx_` 前缀的旧粒子
     材质等,来自会话更早期几次命名重构,一直没人删)。
   - `Ironfield/Post` 自定义全屏 shader(`OnRenderImage`,Built-in 专属回调,
     URP 渲染器根本不会调用它)整个删除,换成 URP 自带的 Volume 后处理框架
     ——`BuildPostVolumeProfile()` 用 `ColorAdjustments`(对比度+11%、
     饱和度+14%,对应旧 shader 的效果)+ `Vignette`(强度 0.26)两个 URP
     内置 Volume Override 做等效的"更有质感"调色,**不需要额外的
     Post-Processing 包**——URP 14+ 已经把 Volume 框架收进核心包了,当初
     "没装 post-processing 包"那条理由已经过时。阴影色调没做等效移植
     (次要细节,无头核对成本高,原样砍掉)。
   - 验证:无头 rebuild 0 编译错误、0 报错;`IronfieldSetup.Screenshot` 全部
     现有截图位重新核对了一遍(载具/无人机/村庄/新增内容全部截图对比,没有
     粉色/紫色缺失 shader 的迹象);另外跑了一次
     `IronfieldSetup.BuildDevPlayer()`——URP 出了名容易在 Build 时把
     Editor 预览用得到、但实际 Build 没引用到的 shader 变体裁掉(shader
     stripping),导致 Editor 里看着对、Build 里变粉——这次构建
     **0 errors 0 warnings**,说明没有被裁掉必需变体。
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
4. ✅ **可访问性**:
   - ✅ 色盲友好目标标记颜色:设置里"色盲模式"开关,把目标框/锁定环/命中
     闪光从红/黄换成蓝/橙(对红绿色盲更安全的经典配色对),默认关闭。
   - ✅ 可关闭镜头震动:设置里"镜头震动"开关,关掉后 `DroneCameraRig.Shake`
     直接不生效。
   - ✅ **UI 缩放选项(2026-09-12 补)**:当初砍掉是因为"要安全做这件事得先
     把 UI 收敛到一个根组件"——后来发现不需要真的重构成一个根组件,只要保证
     每个独立 `OnGUI` 在**任何**提前 `return` 路径上都会恢复矩阵就够了。新增
     `Ironfield.UI.UiScaling.Begin()/End()`,4 个 `OnGUI`(`HudController`/
     `PauseMenu`/`FirstRunTip`/`MainMenuController`)统一改成"`OnGUI` 只做
     `try { DrawGUI(); } finally { UiScaling.End(m); }`,原来的整个方法体挪进
     `DrawGUI()`"——`finally` 保证不管 `DrawGUI` 从哪条路径提前返回,矩阵都会
     被这一个组件自己恢复干净,不会漏给同一帧里排在它后面的下一个 `OnGUI`。
     `GameSettings.UiScale`(PlayerPrefs,范围 0.8-1.4,默认 1)+ 设置面板里
     新增一条"界面缩放"滑条。新增 `GameSettingsTests.cs`(2 条 EditMode:
     默认值、setter 范围钳制)+ `UiScalingTests.cs`(3 条 PlayMode:`Begin`
     确实在 UiScale≠1 时改了矩阵、`End` 精确复原成 `Begin` 捕获的值而不是
     简单重置成单位矩阵、连续两组独立的 `Begin`/`End` 不会互相污染)。
     **没做的验证**:IMGUI 的 `OnGUI` 内容不经过 `Camera.Render()`,这次会话
     一直用的无头截图管线(`IronfieldSetup.Screenshot`)拍不到 HUD/菜单画面,
     所以"缩放后视觉上真的变大了"这件事没法像其它功能一样截图肉眼核对,只
     验证了矩阵数学本身的正确性——这是 IMGUI 覆盖层这类内容在无交互式编辑器
     会话下的已知验证盲区,跟这次会话里其它纯 3D 场景内容的验证方式不同。
5. ✅ **稳定性**:新增 `StabilityTests.cs`(3 条 PlayMode)专门补"切场景/
   暂停不留悬挂状态"这类此前真实踩过的坑——第一条直接回归测试了
   `FirstRunTip` 那次冻结 `Time.timeScale` 搞挂自动化测试的问题类型(断言
   任务场景一加载 `Time.timeScale` 必须是 1、`PauseMenu.IsPaused` 必须是
   false);第二条断言连续三次重开任务场景不会残留重复的
   `MissionManager`/`HudController`/`PauseMenu`;第三条专门测
   `UiSfx`(DontDestroyOnLoad 单例)在"主菜单→任务→主菜单"这种真实会发生
   的来回切换下不会产生重复实例。EditMode 14/14、PlayMode 8/8。

## P3 — 上架准备(如果目标是真的发布)

**状态(2026-09-12):1(仅 Mac)、2、3 已实现;4 留给用户手动做(见下)。**

1. ✅ **构建与分发(仅 Mac)**:`IronfieldSetup.BuildRelease()`(Ironfield 菜单
   A 项)——`BuildOptions.None`(不带 Development 标志),`bundleVersion`
   设成 "0.1.0",真实 bundle identifier,自动生成的方形图标。产物
   `Builds/Release/Ironfield.app`,152 MB,0 errors 0 warnings。**没做
   Windows 构建**:这台机器没装 Windows 平台模块(`PlaybackEngines/` 下只有
   iOS/WebGL),无法无头交叉编译;跟之前 `BuildDevPlayer` 一样的限制。
   实际启动测试(不是无头截图,是真的 `open` 这个 .app 跑起来,用
   computer-use 截图):菜单/制作人员面板/任务内 HUD 全部渲染正确、可操作。
   **发现一个部署摩擦点,记录在 `store/itch_page_text.md` 里**:构建没有
   代码签名/公证(需要付费 Apple Developer 账号,这个项目没有),玩家从
   itch.io 下载后 macOS Gatekeeper 会拦截首次启动,需要"右键→打开"或去
   系统设置里手动放行——已经写进商店页文案,提醒玩家这是正常现象。
2. ✅ **商店素材**:`store/`(gitignore,不进仓库——这是营销交付物不是项目
   源码,跟 `demo_output/`/`Builds/` 一个待遇)。封面 + 5 张精选截图(直接
   复用 `IronfieldSetup.Screenshot` 管线的产出,不是重新截的)、完整商店页
   文案(标题/标签/简介/正文/平台说明,中英都有)、打包好的
   `Ironfield-0.1.0-mac.zip`(51 MB)。**没做 30 秒预告片**:早前 session
   里录过一版 demo 视频,但那是在地图放大、SPAAG/支线目标/URP 迁移这些改动
   *之前*录的,画面已经过时,需要重录——`DemoRecorder`/`DemoRunner` 管线还
   在,重录只是时间成本,不是技术阻碍,留给下一次任务。
3. ✅ **法务**:主菜单新增"制作人员"面板(P3 本次补上,见上一次会话记录),
   随构建物一起可见,不再只存在于仓库的 `CREDITS.md` 里。
4. ⛔ **实际发布到 itch.io——没有替用户做,也不会做**:创建/操作 itch.io
   账号发布内容超出了这次会话能做的范围(账号创建/公开发布这类操作需要用户
   自己在场操作)。已经把全部素材(文案/封面/截图/构建包)整理好交给用户,
   itch.io 页面本身、上传、"发布为公开"这几步需要用户自己在浏览器里完成。
   反馈渠道(评论区/issue 模板)等页面建好之后再补。

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

---

## 阶段外补充:自动攻击(2026-09-11,用户直接提出)

不在原 P0-P3 划分里,按用户要求单独实现:锁定并已经在俯冲攻击目标时,玩家可以
在设置里打开"自动攻击",无人机接管剩余操作、自动完成攻击。

- `GameSettings.AutoAttack` 开关(默认关——手动飞行是这游戏的核心操作乐趣,
  这是可选的便利/无障碍功能,不是默认体验)。
- 复用现有 `DiveAssist` 的介入判定(硬锁定 + 已经大致朝目标俯冲 + 速度/距离
  达标)——开关关闭时行为完全不变(原有的"轻推"辅助);打开后同样的判定条件
  升级成完全接管:`DroneController.AutopilotTarget` 接管瞄准与油门,进入
  开火射程后自动引爆。**只会接管玩家已经主动开始的俯冲,不会凭空抢控制**。
- HUD 锁定环旁边原来的"LOCK"文字,自动攻击接管时换成"AUTO-ATTACK"(绿色)。
- 测试踩坑记录:`DiveAssist`/`TargetingSystem` 是围绕"摄像机朝向+视线+停留
  时间"的有机获取设计的,直接搭个测试场景很难稳定复现"已经对准"的状态——
  给 `TargetingSystem` 加了 `DebugForceLockTarget` 测试钩子(跟已有的
  `DroneController.useDebugInput` 是同一种模式);另外踩了一个 Unity 经典坑:
  给挂了非运动学 Rigidbody 的物体直接设 `transform.position` 会被物理引擎
  下一步同步悄悄改回去,得用 `rigidbody.position`。
- 新增 `AutoAttackTests.cs`(2 条 PlayMode):自动驾驶转向数学的隔离测试,
  和"给定已介入状态,不追加任何输入能否独立完成攻击"的端到端测试。
  EditMode 14/14、PlayMode 10/10。

## 阶段外补充:地图太小 / 很快飞出地图(2026-09-11,用户直接提出)

用户原话:"地图太小,太简单,无人机很短时间就可以飞出地图"。两个问题分开修:

1. **地图本身太小**:`Terrain` 从 1024×1024 扩到 **2048×2048**(面积 4 倍)。
   道路/村庄/发射点保持原有绝对世界坐标不变——只是四周多出了可飞的开阔地,
   不需要把文件里其它几十处硬编码坐标(战场布景、树篱、截图相机位等)跟着
   重新换算。`heightmapResolution`(513→1025)、`alphamapResolution`
   (512→1024)、`baseMapResolution`(1024→2048)、`SetDetailResolution`
   (1024→2048)同步翻倍,保持每米细节不变,不会因为地图变大而变"糊"。
   植被/岩石目标数量从 1000/190 提到 2400/460(不是线性 4 倍——原图靠近道路
   已经够密,scatter 本来就按到路的距离做疏密,4 倍会把新增密度大半堆在村庄
   附近而不是真正帮到边缘的空地)。
   **技术细节**:高度图生成原来用"归一化坐标 nx,ny(0..1)× 固定频率"算噪声,
   这个频率是按 1024m 校准的,直接改 `size` 会让山丘特征跟着变形(频率不变但
   物理宽度翻倍,山会显得又矮又平)。改成直接用世界坐标(米)算噪声频率,
   跟地图大小完全解耦。道路走廊压平原来是"归一化空间里猜一条对角线"的公式
   (`|ny-(0.32+nx*0.30)|`),换成实际路径折线(新增 `RoadPoints` 静态字段,
   `BuildScene` 和 `BuildTerrain` 共用同一份坐标)在世界空间的真实距离
   (`DistanceToPolylineXZ`)——不管地图多大都天然对得上实际道路,不用跟着
   重新拟合系数。
2. **没有边界机制,飞出去是真的飞进空气里**:光靠"地图更大"治标不治本——
   任何有限大小的地图,持续朝一个方向飞总会到边缘。新增地图边界系统:
   `MissionManager.SpawnDrone` 生成无人机时从**当前场景实际的 Terrain 边界**
   算出中心点/软半径/硬半径(不是写死数值,3 关地形一样大小但以防将来某关
   改地图大小,这里天然适配),写进 `DroneController` 新增的
   `boundaryCentre`/`boundarySoftRadius`/`boundaryHardRadius`。
   - 软边界(离硬边界还有 140m 缓冲区)内没有任何变化,正常飞。
   - 越过软边界:HUD 顶部弹出脉动警告"⚠ 返回战斗区域 · LEAVING COMBAT
     AREA",同时一股跟越界程度成正比的力把无人机往地图中心推——手感上更像
     顶风,不是硬拽,越贴着边界飞推力越强。
   - 硬边界(离地图实际边缘还留 40m)是无人机永远无法跨过的墙:哪怕全程
     boost 顶着往外冲,`FixedUpdate` 每帧都把水平位置钳回硬边界圆周上、
     抵消掉朝外的速度分量,不会瞬移闪现,是每帧连续钳制。
   - 默认半径是个巨大的"实质关闭"值,不挂 `MissionManager` 的孤立测试场景
     不会意外触发。
   - 新增 `DroneBoundaryTests.cs`(2 条 PlayMode):一条验证生成的无人机确实
     拿到了从实际地形算出的合理半径(不是默认的巨大值);一条真的把无人机
     瞬移到硬边界外 300m、冻结输入、跑几帧物理,断言最终位置被钳回边界内。
   EditMode 14/14、PlayMode 14/14(12 + 2 条新增)。

## 阶段外补充:内容密度(2026-09-12,用户直接提出)

地图放大之后内容跟着做了一轮加密,按用户选的四个方向都做了(额外可选目标为
主、地标、敌方种类、关卡地形差异化):

1. **额外可选目标**:新的 `RadioOutpost`(无线电中继站)——`Vehicle.optional`
   标记,`VehicleRegistry` 新增 `OptionalTotal`/`OptionalDestroyed` 与主力车队
   分开计数,`MissionManager.VehiclesTotal`/`Killed` 只算主力车队,不会因为
   有额外目标没打就赢不了/输不了。摧毁给独立的 400 分奖励(`_bonusTargetScore`),
   HUD 新增 `BONUS x/y` 行,结算面板也单列一行。血量只有 45(比任何主力车辆
   都脆),没有武装(没挂 `VehicleTurret`)——纯粹是"值得绕路"的奖励,不是
   威胁。3 关各放 2-3 个,散布在车队路线两侧远处(离地图边界软半径还有余量,
   保证飞得到)。
2. **地标/侦查点**:新的 `Watchtower`(瞭望塔)——纯环境物件,没有
   Vehicle/HealthComponent,不能打、不算目标,只是给放大后的地图一个能从远处
   认路的视觉参照物,3 关共用同 2 个位置。
3. **敌方种类更多**:新的 `VehicleClass.SPAAG`(自行高炮)。复用 IFV 的
   CC-BY 外部模型作为车体(没有另外找新的 CC0 模型,风险类型跟当初找车辆
   模型时一样),视觉区分靠 `BuildSpaagTopside` 加的双联装炮管+雷达碟(挂载
   在车顶,cosmetic-only,不影响 `VehicleTurret` 的实际开火逻辑)+跟坦克一样
   的 `Militarize` 染色。数值上是"火力覆盖型"而不是"重炮偷袭型":射程 230
   (IFV 150)、开火间隔 0.07s(IFV 0.11s)、连发 8 发(IFV 5发)、单发伤害只
   有 3.5(IFV 4.5)——总 DPS 更高但更依赖玩家"别在附近逗留",跟坦克"偶尔
   一记重拳"的手感刻意做出区别。Mission02 换 1 辆 IFV、Mission03 换 2 辆
   IFV,车队总规模不变(6→7→8 依旧成立)。
4. **关卡地形差异化**:见上面 P1 第 1 条的更新——`terrainSeedOffset` 让 3 关
   山丘/纹理斑块/森林分布采样噪声场里不同的相位(不是重新设计噪声函数,只是
   让 3 关"从同一张噪声图里截取不同区域"),`dryness` 让后两关草地比例降低、
   干草/焦黑比例升高,`vegetationDensityMul`(1.0→0.85→0.68)让后两关植被更
   稀疏,`moodTint` 给光照/雾色整体调一层暖黄/灰烬色。Mission01 全部参数保持
   "无变化"默认值,像素级维持原样(不影响已有截图/记忆里对 Mission01 外观
   的描述)。
   - **范围调整**:没做天气/时段系统(晴↔阴、白天↔黄昏),只做了噪声相位+
     色调+密度这三个"轻量能落地"的差异化维度。
5. 新增 `ContentDensityTests.cs`(3 条 PlayMode):验证 Mission01 的
   `VehiclesTotal` 只算主力车队(不含 2 个 bonus target)、验证摧毁一个
   bonus target 不会结束任务且正确记进 `BonusKilled`、验证 Mission02 的
   SPAAG 开火间隔确实比 IFV 快。EditMode 14/14、PlayMode 17/17
   (14 + 3 条新增)。

## 阶段外补充:多语言本地化(2026-09-12,用户直接提出)

用户原话:"游戏要改成支持英语、法语、日语、德语、西班牙语以及简体中文和繁体
中文，默认语言是英语"。上架前才想起这件事,补上了。

- 新增 `Ironfield.Core.Loc`——项目里没有 CSV/JSON 导入管线(跟这个项目
  "所有东西都是代码生成"的一贯做法一致,无头环境搭一套外部数据导入也没有
  交互式会话去调试),就用一个 `Dictionary<string, string[]>` 存翻译表,
  数组下标对应 `Language` 枚举顺序(en/fr/ja/de/es/简/繁)。`Loc.Get(key)`
  按 `GameSettings.Language` 取对应语言,缺失或空字符串自动回退英语;传入
  一个不存在的 key 直接把 key 本身显示出来(方便一眼看出漏翻的地方,不会
  静默显示空白)。
- `GameSettings.Language`(PlayerPrefs 持久化,默认 `Language.English`——
  按用户要求,不跟系统语言自动检测挂钩)+ 设置面板新增"Language"一行,
  点击在 7 种语言间循环切换,语言名本身固定显示成"English/Français/
  日本語/Deutsch/Español/简体中文/繁體中文"这种各语言自己的说法(不经过
  `Loc.Get` 翻译)——这样不管当前界面是哪种语言,玩家都找得到自己那行。
- **改了全部 5 个 IMGUI 界面文件**(`MainMenuController`/`SettingsGUI`/
  `PauseMenu`/`FirstRunTip`/`HudController`)——菜单/设置/暂停/操作说明/
  出击须知/HUD 状态栏/瞄准提示/结算面板,一处硬编码字符串都没留。
  `MissionCatalog.DisplayName` 和 `Vehicle.displayName`(载具锁定框显示的
  名字)也从字面文本改成存 Loc key,在绘制那一刻才 `Loc.Get()`——`Vehicle.
  displayName` 是 `IronfieldSetup.cs` 无头预烘焙进 prefab 的,如果直接存
  翻译好的文本,运行时切语言根本改不动已经烘焙进场景的字符串,必须存 key。
- 新增 `LocTests.cs`(5 条 EditMode,核心是一条"翻译表里每个 key 的 7 种
  语言都不能有空/缺项"的完整性检查——不然某个 key 漏翻一种语言会一直静默
  回退英语,没有任何信号提醒去补)+ `LocIntegrationTests.cs`(1 条
  PlayMode:加载 Mission03,断言场上每个载具的 `displayName` 都能在 Loc
  表里查到真实译文,不是原样把 key 显示出来——防止 `IronfieldSetup.cs` 烘焙
  的 key 字符串和 `Loc.cs` 定义的 key 对不上而两边都不报错的哑巴问题)。
- **实机验证**(不是无头截图——IMGUI 内容走不了 `Camera.Render()` 那条无头
  截图管线,这个盲区在 UI 缩放那次就记过一次):真的 `open` 了构建出来的
  .app,用 computer-use 截了英/法/日/简中/繁中五种语言的设置面板和主菜单,
  全部正确渲染,日文假名/汉字、繁简中文都没有缺字型/方框。发现一个真实的
  排版问题——法语/德语翻译比原文长,无人机选择卡片(原来 210px 宽)在法语
  下"Dégâts élevés"这行被裁切,加宽到 260px 修复。其它按钮/面板宽度没有
  逐个用超长语言字符串再核对一遍(时间成本高),如果之后发现某处被裁切,
  同样的加宽思路照抄。
- EditMode 21/21(16 + 5 条新增)、PlayMode 22/22(21 + 1 条新增)。

## 阶段外补充:自动攻击撞树/撞电线杆卡住(2026-09-12,用户实测反馈)

用户原话:"无人机进入自动模式攻击敌军装备时,不能只能规划路线,经常前方有
障碍比如树木、电线杆就卡在哪里了"。`UpdateAutopilotAim`(`DroneController.cs`)
原来就是纯"对准目标直飞"的比例控制,对路上有什么完全没有感知——地图上散布着
2000+ 棵树和电线杆(`ScatterVegetation`/`ScenePropsPass`),自动攻击飞一段
距离后迟早会撞上一棵,而原逻辑会不停下达"继续往同一个方向飞"的指令,把无人机
死死顶在障碍物上。

- 新增 `DroneController.ComputeAvoidance()`:朝无人机当前**朝向(仅偏航角,
  不含俯仰)**做水平探测射线(中心+左右各偏 0.6 弧度一条),只打
  Environment 层(地形/树/电线杆/建筑——绝不会是载具,所以永远不会因为这套
  逻辑而绕开真正的攻击目标)。中心射线探测到东西:默认爬升越过(多数单棵树/
  电线杆矮于无人机能在探测提前量内爬升的高度),同时用左右射线判断哪一侧更
  空,朝空的一侧偏航。
- **踩了两个坑,靠对比多轮测试结果才定位到**:
  1. 探测射线一开始用的是 `transform.forward`(含俯仰的完整 3D 朝向)——
     但真实俯冲攻击时无人机本来就该朝目标(通常贴地)低头飞,这条射线会把
     "目标附近的地面"也判断成"前方有障碍",导致每一次正常俯冲攻击都被
     "爬升避障"打断,`Auto_attack_setting_finishes_a_committed_dive_on_its_own`
     测试直接回归失败。改成只用偏航角构造的水平探测方向,不随俯仰联动,
     才把"真的有东西挡路"和"本来就该俯冲"这两件事分开。
  2. **进入 20m 终段攻击距离内整套避障直接关闭**(`DiveAssist` 的自动开火
     距离是 6.5m,20m 留了充足余量):目标本身通常就贴着地面/障碍物旁边,
     不关掉的话最后一段俯冲同样会被误判成"障碍物"而被打断。
  3. 加了一道兜底:`_stuckTimer` 持续追踪速度是否长期低于 3,超过 0.6 秒
     不管射线怎么说都强制爬升+转向,防止探测射线的窄锥角漏掉的贴边碰撞
     情况。
  4. **调试时发现的一个假象,记录下来避免以后重复踩**:第一版回归测试反复
     卡在完全相同的"90m 目标只前进 28m"结果,不管怎么调参数都不变——
     一度以为是重力/边界系统之类别的因素在起作用,加了详细的逐秒日志之后
     才发现问题根本不在避障逻辑本身:测试直接手动设置了
     `drone.AutopilotTarget`,但没有关掉 `DiveAssist`——`DiveAssist.
     FixedUpdate()` 在没有真实硬锁定的情况下每一帧都会把
     `AutopilotTarget` 清空(这是这次会话更早记录过的已知坑:"任何直接
     驱动 AutopilotTarget 的调用者必须先关掉 DiveAssist"),导致自动驾驶
     瞄准值被冻结在 (0,0) 后完全停止响应,无人机只是单纯静止不动、并非
     真的被物理卡住。**关掉测试自身的 `DiveAssist` 之后,才看到真正的
     信号**:无人机确实还在撞障碍物,只是这次是因为响应太慢导致直接高速
     撞上、被撞击伤害打死——顺着这个线索才把探测提前量(`lookAhead`)
     进一步调大到足够早爬升,不再发生碰撞。
- `AutoAttackTests.cs` 新增 `AutopilotTarget_makes_progress_past_a_direct_obstacle`
  (1 条 PlayMode):在无人机与目标连线正中间放一个跟游戏里真实树木碰撞体
  尺寸相当的障碍(约 6×7×3),断言 6.7 秒内无人机朝目标推进的距离超过一半、
  且没有被撞死。
- EditMode 21/21、PlayMode 23/23(22 + 1 条新增)。

## 阶段外补充:自动巡航(2026-09-12,用户直接提出)

用户原话:"给无人增加自动巡航模式,比如按键"A",就自动巡航"。

- 新增 `Ironfield.Drone.CruiseAssist`——按 A(手柄 X)切换,开启后自动朝
  **最近的存活载具**飞,复用跟自动攻击同一套会避障的 `DroneController.
  AutopilotTarget` 转向逻辑(直接受益于这次会话刚做的撞树/撞电线杆修复)。
  一旦真正形成硬锁定(`TargetingSystem.HasHardLock`),巡航立刻交出控制权——
  接下来是玩家手动瞄准,还是 `DiveAssist`/自动攻击接管终段俯冲,由已有系统
  决定,巡航自己**永远不会引爆战斗部**,纯粹是"帮你飞到目标附近"的导航辅助。
  等这次交战结束(锁定丢失或目标被摧毁),巡航会自动去找下一个最近的目标,
  一直到玩家再按一次 A 关掉为止。
- `DroneInput.cs` 新增 A 键(键鼠)/ X 键(手柄)读取;`DroneController` 新增
  `CruiseToggleRequested` 事件(跟现有 `FireRequested`/`RecallRequested`
  同一套模式);`MissionManager.SpawnDrone` 给每个刷新出来的无人机自动挂上
  `CruiseAssist`,同 `DiveAssist` 一起。HUD 左下角(速度/高度旁)开启时会显示
  一个"CRUISE"脉动提示;操作说明面板(暂停菜单/出击须知/HUD 右下角控制提示)
  三处共用的 `PauseMenu.ControlLines()` 都加了这一行,支持全部 7 种语言。
- **开发过程中顺手修了一个更早就存在的真 bug,不是这次巡航功能引入的**:
  写巡航回归测试时,之前稳定通过的 `Auto_attack_setting_finishes_a_
  committed_dive_on_its_own` 突然开始稳定失败——加详细日志排查后发现跟
  `CruiseAssist` 毫无关系(`cruiseEngaged` 全程是 `False`),真正原因是
  `DiveAssist.Engaged()` 的"最低速度"门槛(`minSpeed=10`)**在距离判定之前
  就无条件生效**:无人机在距离目标 13 米左右(还没到 6.5 米的自动开火距离)
  速度掉到了 10 以下,直接被判定为"脱离交战",之后再也没有重新拿回
  `AutopilotTarget`,永远停在原地不开火。这是那种"时好时坏"的边界情况——
  换一次无关的代码改动就可能因为极小的时序差异触发或不触发,之前只是运气好
  一直没撞上。修了两处:①已经进入 6.5 米自动开火距离内,不管当前速度/朝向
  如何一律直接开火,不再检查最低速度;②加了迟滞(hysteresis)——一旦真正
  进入过交战状态,后续的速度/夹角门槛放宽到原来的 35%/175%,不会因为(比如)
  车队目标本身还在移动带来的瞬间速度/角度抖动就立刻脱离交战,减少这种
  "刚进入又立刻退出"的抖动。
- `AutoAttackTests`/`CruiseAssistTests` 合计新增 3 条 PlayMode:验证按 A
  会朝最近载具转向并实际缩短距离、再按一次 A 会立刻交还控制权、真正硬锁定后
  巡航不再覆盖 `AutopilotTarget`(用一个巡航不可能主动选中的哨兵坐标验证,
  排除"巧合选中同一个值"的假阳性)。
- EditMode 21/21、PlayMode 26/26(23 + 3 条新增)。

## 阶段外补充:实测反馈两处修复(2026-09-12,用户实测自动巡航后提出)

用户原话:"我测试了一下，A（自动）自动模式下，无人机高度下降的太多，视觉效果
感觉无人机是在贴着地面飞行。此外，无人机的速率操控范围太小。加速、减速都在
很小的范围之内，需要调整"。

1. ✅ **巡航模式贴地飞**:`CruiseAssist` 原来直接把 `AutopilotTarget` 设成
   `target.AimPoint`(载具中部高度,离地就几米),自动驾驶的俯仰会直接对准
   这个点——哪怕无人机离目标还很远,也会立刻低头下潜,视觉上就是"贴着地面
   飞"。改成巡航阶段瞄准目标正上方 22 米的点(`cruiseAltitude` 字段,可调),
   飞到目标附近、真正形成硬锁定后巡航就交出控制权,由 `DiveAssist`/玩家
   接手真正的俯冲——这一段本来就该低头下探,不受这次改动影响。
2. ✅ **加速/减速范围太窄,真找到了一个计算 bug**:`DroneController.
   FixedUpdate` 里油门到速度的映射,W 半段原来是
   `Lerp(cruiseFraction, 1, Clamp01(0.5+0.5*throttle))`——`throttle=0`
   (不按键,松手)时 `Clamp01(0.5+0.5*0)=0.5`,也就是**松手状态本身就已经
   停在 71% 速度**(`Lerp(0.42,1,0.5)`),W 键能往上加的空间只剩 71%→100%
   这 29 个百分点,手感上自然是"怎么加速都差不多"。改成直接用 `throttle`
   本身(已经是 -1..1)做插值:`throttle≥0` 时 `Lerp(cruiseFraction,1,
   throttle)`,松手真正停在 `cruiseFraction`(42%),W 键能加满到 100%
   (58 个百分点的可用范围,是原来的两倍)。S 刹车那一段原来就是对的
   (`Lerp(cruiseFraction,0.12,-throttle)`),没有改。
3. 没有新增自动化测试——这两处都是数值/瞄准点调整,不是新的分支逻辑,已有的
   `AutopilotTarget_steers_the_drone_toward_it`/`CruiseAssistTests` 覆盖了
   "自动驾驶确实会转向目标"这条主干行为,数值本身建议下次实机验证时留意手感。
   EditMode/PlayMode 数量不变(21/26)。

## 阶段外补充:速率范围仍不够宽 + 植被密度/精细度(2026-09-13,用户实测反馈)

用户原话:"试玩了一下，改善不多，此外地面不同植物的模型还需要精细化，地面
植被也太过于稀疏，很多植物单株零零散散，空地也很多"。

1. ✅ **速率范围二次调整**:上一条修复把公式改对了,但绝对基准
   `cruiseFraction=0.42` 本身离满速还是太近,导致 W/S 的可用范围主观上依然
   偏窄。直接下调基准:`cruiseFraction` 0.42→0.22,S 刹车下限 0.12→0.05
   (`DroneController.cs`)。松手巡航速度降到 22% 满速,W 能加到 100%
   (78 个百分点可用范围,是上一版的 1.34 倍、原始版本的 2.7 倍),S 能减到
   5% 接近悬停。**副作用**:`AutopilotTarget_steers_the_drone_toward_it`
   (`AutoAttackTests.cs`)原本要求 2.5 秒内 alignment>0.5,巡航速度降低后
   无人机转弯时携带的动量变小,对着一个左右偏移(60m)远大于前向偏移(40m)
   的急转弯目标,alignment 稳定卡在 0.48-0.49(延长模拟时间到 3.7 秒也没有
   明显改善,确认是转弯几何变化不是时序问题),说明无人机确实还在转向目标、
   只是新调校下这类极端侧偏角的对齐上限更低——阈值相应下调到 0.4,并在测试
   里写明原因。
2. ✅ **植被密度修复,顺带修了一个真实的分布 bug**:`ScatterVegetation`
   (`IronfieldSetup.cs`)原逻辑里,前 960 棵树(`vegetationDensityMul` 缩放后)
   是完全无视"树林/空地"噪声值直接铺的,只有超过这个数量后新增的树才会看
   `woods<0.06` 的空地阈值——也就是说不管噪声图怎么分布,地图上一定先铺出一层
   均匀撒开的稀疏背景树,再在此基础上叠加真正的树林聚集,这正是"单株零零散散、
   到处都是空地"的观感来源。改成从第一棵树开始就统一用同一条密度曲线判定要不要
   放:噪声频率从 0.010 降到 0.006(聚落尺度更大,更容易读出"这是一片林子"而不是
   噪点),值域从线性改成先映射到 [0,1] 再平方(`density=density*density`),
   让分布明显两极化——要么落在"肯定是林子"的高密度区,要么落在"肯定是空地"的
   低密度区,不再有大片中间态的稀疏过渡带。目标数量本身也上调:
   `treeTarget` 2400→5200、`rockTarget` 460→700(乘以 `vegetationDensityMul`
   不变),这依据的是本 session 更早验证过的性能余量(无上限帧率在 130+ fps)。
   "模型需要精细化"这一条:`BuildFoliagePrefab` 除 Bush 外的阔叶/针叶/枯树都已
   经用的是外部 CC0/CC-BY 真实树木模型(不是程序化占位),Bush 仍是程序化的——
   没有更好的免费素材来源之前,这块的进一步精细化超出这次修复的范围,先如实
   记录在案。
3. 没有新增自动化测试(密度/分布是数值与美术调校,已有的场景构建流程
   `IronfieldSetup.Run()` 每次都会重新生成验证不报错;`AutoAttackTests.cs`
   里因速率调校下调的阈值算作对既有测试的必要更新)。EditMode/PlayMode:
   21/26,全绿。已通过 Release build 重新打包验证编译与场景生成无误;因用户
   屏幕处于锁屏状态,肉眼确认植被观感/手感这一步待用户本人解锁后试玩确认。
4. ⚠️ **上面这版密度修复本身还带了一个真实 bug,推送时才暴露**:密度提高后
   `git push` 被 GitHub 拒绝——`Mission01/02/03.unity` 涨到 112-120MB,超过
   100MB 硬限制(之前约 29MB)。根因:新的聚类逻辑只靠外层 `while` 循环的
   `treeN < treeTarget` 判断收尾,没有在放置树的分支里显式判断
   `treeN >= treeTarget` 就 continue——旧逻辑接受率接近 100%,基本不会绕出这
   个问题;新逻辑接受率大幅降低后,循环要跑远更多次才能让 `treeN` 达标,
   等待 `rockN` 追上的这段时间里树会一直被无上限地继续放。补上显式判断后
   重新生成:三关场景反而比修复前(2400 棵树版本)更小——18-25MB,因为新
   聚类不再把大量点浪费在"中等密度但既不算林子也不算空地"的过渡带里。
   由于这一版有问题的提交(`fd35351`)从未成功推送到远端(GitHub 直接拒收),
   用 `git reset --soft` 把它和紧接着的修复提交合并成一个干净提交
   (`2fa61a1`)一次性推送,没有往远端写入任何坏的中间状态。
   **教训**:任何"外层循环靠双变量共同判断退出"的采集/生成循环,每个变量
   自己的放置分支都要有一份显式上限判断,不能只指望外层条件——尤其是当某
   个变量的"接受率"后续被调低时,旧代码在旧接受率下"恰好没出问题"不代表
   逻辑本身是对的。
