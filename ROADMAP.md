# Ironfield — 从原型到可交付产品的路线图

现状(2026-09-11):三关 `Mission01-03`,IMGUI 主菜单/HUD,两种无人机(轻型/
重型,打通 Mission01 解锁重型)、三种载具、关卡进度存档,音频接了部分真实
CC0 录音,Built-in 渲染管线未做美术终审。可玩、可通关/失败,内容量比早期的
单关竖切片厚不少,但美术/音效终审和上架准备(P2/P3)还没做。

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
