using System.Collections.Generic;

namespace Ironfield.Core
{
    public enum Language
    {
        English, French, Japanese, German, Spanish,
        ChineseSimplified, ChineseTraditional,
    }

    /// <summary>
    /// Tiny in-code localization table — matches this project's "everything
    /// generated, nothing loaded from external data files" convention (no CSV/
    /// JSON import pipeline exists here, and headless batchmode has no good way
    /// to author one interactively). GameSettings.Language picks the active
    /// column; Get() falls back to English (index 0) for any missing/empty
    /// entry so a partial translation never shows a blank label.
    /// </summary>
    public static class Loc
    {
        public static string Get(string key)
        {
            if (Table.TryGetValue(key, out var arr))
            {
                int i = (int)GameSettings.Language;
                if (i >= 0 && i < arr.Length && !string.IsNullOrEmpty(arr[i])) return arr[i];
                return arr[0];
            }
            return key; // missing key: show the key itself, a visible signal to fix it
        }

        /// <summary>Formats like string.Format, after localizing the key.</summary>
        public static string Get(string key, params object[] args) => string.Format(Get(key), args);

        static readonly int LanguageCount = System.Enum.GetValues(typeof(Language)).Length;

        /// <summary>Test/tooling hook: every key whose translation array is
        /// missing an entry (wrong length, or empty/null string) for at least
        /// one language — should always be empty. Not used by gameplay code.</summary>
        public static IEnumerable<string> KeysWithMissingTranslations()
        {
            foreach (var (key, arr) in Table)
            {
                if (arr.Length != LanguageCount) { yield return key; continue; }
                for (int i = 0; i < arr.Length; i++)
                    if (string.IsNullOrEmpty(arr[i])) { yield return key; break; }
            }
        }

        /// <summary>Each language's own name for itself — deliberately NOT run
        /// through Get()/the current language, so a player can always find
        /// their language on the list even if the UI is currently in one they
        /// don't read.</summary>
        public static readonly string[] LanguageNames =
        {
            "English", "Français", "日本語", "Deutsch", "Español", "简体中文", "繁體中文",
        };

        // columns: en, fr, ja, de, es, zh-Hans, zh-Hant
        static readonly Dictionary<string, string[]> Table = new()
        {
            // ---- main menu -------------------------------------------------
            ["menu.subtitle"] = new[]
            {
                "Drone strike operations — a fictional Eastern European front",
                "Frappes de drones — un front fictif d'Europe de l'Est",
                "ドローン強襲・架空の東欧戦線",
                "Drohnenangriffe — eine fiktive osteuropäische Front",
                "Ataques con drones — un frente ficticio de Europa del Este",
                "无人机突袭 · 一片虚构的东欧战线",
                "無人機突襲 · 一片虛構的東歐戰線",
            },
            ["menu.start"] = new[] { "Start Mission", "Commencer la mission", "任務開始", "Mission starten", "Iniciar misión", "开始任务", "開始任務" },
            ["menu.settings"] = new[] { "Settings", "Options", "設定", "Einstellungen", "Ajustes", "设置", "設置" },
            ["menu.credits"] = new[] { "Credits", "Crédits", "クレジット", "Mitwirkende", "Créditos", "制作人员", "製作人員" },
            ["menu.quit"] = new[] { "Quit", "Quitter", "終了", "Beenden", "Salir", "退出", "退出" },
            ["menu.footer"] = new[]
            {
                "prototype v0.1 — MIT licensed, third-party model credits in CREDITS.md",
                "prototype v0.1 — licence MIT, crédits des modèles tiers dans CREDITS.md",
                "プロトタイプ v0.1 — MITライセンス、サードパーティモデルのクレジットはCREDITS.mdに記載",
                "Prototyp v0.1 — MIT-Lizenz, Drittanbieter-Modellnachweise in CREDITS.md",
                "prototipo v0.1 — licencia MIT, créditos de modelos de terceros en CREDITS.md",
                "prototype v0.1 — MIT 许可，第三方模型署名见 CREDITS.md",
                "prototype v0.1 — MIT 授權，第三方模型署名見 CREDITS.md",
            },

            // ---- common / shared buttons ------------------------------------
            ["common.back"] = new[] { "Back", "Retour", "戻る", "Zurück", "Volver", "返回", "返回" },

            // ---- mission select ----------------------------------------------
            ["missions.title"] = new[] { "Select Mission", "Choisir une mission", "任務選択", "Missionsauswahl", "Seleccionar misión", "选择任务", "選擇任務" },
            ["missions.locked_hint"] = new[]
            {
                "Clear the previous mission to unlock",
                "Terminez la mission précédente pour débloquer",
                "前のミッションをクリアすると解放されます",
                "Schließe die vorherige Mission ab, um freizuschalten",
                "Supera la misión anterior para desbloquear",
                "先打通上一关解锁",
                "先打通上一關解鎖",
            },
            ["missions.not_cleared"] = new[] { "Not cleared yet", "Pas encore terminée", "未クリア", "Noch nicht abgeschlossen", "Aún no superada", "尚未通关", "尚未通關" },
            ["missions.best_score"] = new[]
            {
                "Best score {0}    Grade {1}",
                "Meilleur score {0}    Note {1}",
                "ベストスコア {0}　評価 {1}",
                "Bestwert {0}    Note {1}",
                "Mejor puntuación {0}    Calificación {1}",
                "最佳评分 {0}    评级 {1}",
                "最佳評分 {0}    評級 {1}",
            },

            // ---- drone picker --------------------------------------------
            ["drone.light.name"] = new[] { "Light Drone", "Drone léger", "軽量無人機", "Leichte Drohne", "Dron ligero", "轻型无人机", "輕型無人機" },
            ["drone.light.sub"] = new[] { "Standard · Agile", "Standard · Agile", "標準・敏捷", "Standard · Wendig", "Estándar · Ágil", "标准 · 敏捷", "標準 · 敏捷" },
            ["drone.heavy.name"] = new[] { "Heavy Drone", "Drone lourd", "重量無人機", "Schwere Drohne", "Dron pesado", "重型无人机", "重型無人機" },
            ["drone.heavy.sub"] = new[]
            {
                "Heavy armour · High damage · Slower",
                "Blindage lourd · Dégâts élevés · Plus lent",
                "重装甲・高火力・低速",
                "Schwere Panzerung · Hoher Schaden · Langsamer",
                "Blindaje pesado · Alto daño · Más lento",
                "重装甲 · 高伤害 · 较慢",
                "重裝甲 · 高傷害 · 較慢",
            },
            ["drone.heavy.locked"] = new[]
            {
                "Clear Mission 01 to unlock",
                "Terminez la mission 01 pour débloquer",
                "ミッション01をクリアすると解放されます",
                "Schließe Mission 01 ab, um freizuschalten",
                "Supera la misión 01 para desbloquear",
                "打通任务01解锁",
                "打通任務01解鎖",
            },
            ["drone.light.short"] = new[] { "Light", "Léger", "軽量", "Leicht", "Ligero", "轻型", "輕型" },
            ["drone.heavy.short"] = new[] { "Heavy", "Lourd", "重量", "Schwer", "Pesado", "重型", "重型" },
            ["drone.recon.name"] = new[] { "Recon Drone", "Drone de reconnaissance", "偵察無人機", "Aufklärungsdrohne", "Dron de reconocimiento", "侦察无人机", "偵察無人機" },
            ["drone.recon.sub"] = new[]
            {
                "Fixed-wing · Fast · Fragile",
                "Voilure fixe · Rapide · Fragile",
                "固定翼・高速・脆弱",
                "Starrflügler · Schnell · Zerbrechlich",
                "Ala fija · Rápido · Frágil",
                "固定翼 · 高速 · 脆弱",
                "固定翼 · 高速 · 脆弱",
            },
            ["drone.recon.locked"] = new[]
            {
                "Clear Mission 02 to unlock",
                "Terminez la mission 02 pour débloquer",
                "ミッション02をクリアすると解放されます",
                "Schließe Mission 02 ab, um freizuschalten",
                "Supera la misión 02 para desbloquear",
                "打通任务02解锁",
                "打通任務02解鎖",
            },
            ["drone.recon.short"] = new[] { "Recon", "Reco", "偵察", "Aufkl.", "Reco", "侦察", "偵察" },

            // ---- mission catalog display names -----------------------------
            ["mission.m01.name"] = new[] { "01 · Ridge Recon", "01 · Reconnaissance de crête", "01・稜線偵察", "01 · Grat-Aufklärung", "01 · Reconocimiento de cresta", "01 · 侦察脊线", "01 · 偵察脊線" },
            ["mission.m02.name"] = new[] { "02 · Road Ambush", "02 · Embuscade routière", "02・街道殉道", "02 · Straßenhinterhalt", "02 · Emboscada en la carretera", "02 · 公路殉道", "02 · 公路殉道" },
            ["mission.m03.name"] = new[] { "03 · The Column", "03 · La colonne", "03・指揮縦隊", "03 · Die Kolonne", "03 · La columna", "03 · 指挥纵队", "03 · 指揮縱隊" },

            // ---- credits panel ------------------------------------------
            ["credits.title"] = new[] { "Credits", "Crédits", "クレジット", "Mitwirkende", "Créditos", "制作人员 · Credits", "製作人員 · Credits" },
            ["credits.subtitle"] = new[]
            {
                "Full third-party attributions in the repo's CREDITS.md",
                "Attributions tierces complètes dans CREDITS.md du dépôt",
                "サードパーティ素材の完全な一覧はリポジトリの CREDITS.md を参照",
                "Vollständige Drittanbieter-Nachweise in CREDITS.md des Repos",
                "Atribuciones completas de terceros en CREDITS.md del repositorio",
                "第三方素材完整清单见仓库 CREDITS.md",
                "第三方素材完整清單見倉庫 CREDITS.md",
            },
            ["credits.drone"] = new[]
            {
                "Drone model \"Drone\" — NateGazzard (poly.pizza)",
                "Modèle de drone \"Drone\" — NateGazzard (poly.pizza)",
                "ドローンモデル「Drone」— NateGazzard（poly.pizza）",
                "Drohnenmodell \"Drone\" — NateGazzard (poly.pizza)",
                "Modelo de dron \"Drone\" — NateGazzard (poly.pizza)",
                "无人机模型 \"Drone\" — NateGazzard (poly.pizza)",
                "無人機模型 \"Drone\" — NateGazzard (poly.pizza)",
            },
            ["credits.tank"] = new[]
            {
                "Tank model \"Tank\" — Nico _ (poly.pizza)",
                "Modèle de char \"Tank\" — Nico _ (poly.pizza)",
                "戦車モデル「Tank」— Nico _（poly.pizza）",
                "Panzermodell \"Tank\" — Nico _ (poly.pizza)",
                "Modelo de tanque \"Tank\" — Nico _ (poly.pizza)",
                "坦克模型 \"Tank\" — Nico _ (poly.pizza)",
                "戰車模型 \"Tank\" — Nico _ (poly.pizza)",
            },
            ["credits.ifv"] = new[]
            {
                "IFV model \"Super Tank\" — Zsky (poly.pizza)",
                "Modèle de VCI \"Super Tank\" — Zsky (poly.pizza)",
                "歩兵戦闘車モデル「Super Tank」— Zsky（poly.pizza）",
                "Schützenpanzer-Modell \"Super Tank\" — Zsky (poly.pizza)",
                "Modelo de IFV \"Super Tank\" — Zsky (poly.pizza)",
                "步兵战车模型 \"Super Tank\" — Zsky (poly.pizza)",
                "步兵戰車模型 \"Super Tank\" — Zsky (poly.pizza)",
            },
            ["credits.truck"] = new[]
            {
                "Truck model — Alex Safayan (poly.pizza)",
                "Modèle de camion — Alex Safayan (poly.pizza)",
                "トラックモデル — Alex Safayan（poly.pizza）",
                "LKW-Modell — Alex Safayan (poly.pizza)",
                "Modelo de camión — Alex Safayan (poly.pizza)",
                "卡车模型 — Alex Safayan (poly.pizza)",
                "卡車模型 — Alex Safayan (poly.pizza)",
            },
            ["credits.trees_by"] = new[]
            {
                "Conifer / dead-tree models — Danni Bittman (poly.pizza)",
                "Modèles de conifères / arbres morts — Danni Bittman (poly.pizza)",
                "針葉樹・枯れ木モデル — Danni Bittman（poly.pizza）",
                "Nadelbaum-/Totholz-Modelle — Danni Bittman (poly.pizza)",
                "Modelos de coníferas / árboles muertos — Danni Bittman (poly.pizza)",
                "针叶树 / 枯树模型 — Danni Bittman (poly.pizza)",
                "針葉樹 / 枯樹模型 — Danni Bittman (poly.pizza)",
            },
            ["credits.broadleaf"] = new[]
            {
                "Broadleaf tree model \"Common Tree\" — Quaternius (poly.pizza)",
                "Modèle d'arbre feuillu \"Common Tree\" — Quaternius (poly.pizza)",
                "広葉樹モデル「Common Tree」— Quaternius（poly.pizza）",
                "Laubbaum-Modell \"Common Tree\" — Quaternius (poly.pizza)",
                "Modelo de árbol de hoja ancha \"Common Tree\" — Quaternius (poly.pizza)",
                "阔叶树模型 \"Common Tree\" — Quaternius (poly.pizza)",
                "闊葉樹模型 \"Common Tree\" — Quaternius (poly.pizza)",
            },
            ["credits.buildings"] = new[]
            {
                "Building models — Kenney / Quaternius (poly.pizza)",
                "Modèles de bâtiments — Kenney / Quaternius (poly.pizza)",
                "建物モデル — Kenney / Quaternius（poly.pizza）",
                "Gebäudemodelle — Kenney / Quaternius (poly.pizza)",
                "Modelos de edificios — Kenney / Quaternius (poly.pizza)",
                "建筑模型 — Kenney / Quaternius (poly.pizza)",
                "建築模型 — Kenney / Quaternius (poly.pizza)",
            },
            ["credits.sfx"] = new[]
            {
                "Sound effects — Kenney.nl (Sci-fi / Impact / Interface Sounds)",
                "Effets sonores — Kenney.nl (Sci-fi / Impact / Interface Sounds)",
                "効果音 — Kenney.nl（Sci-fi / Impact / Interface Sounds）",
                "Soundeffekte — Kenney.nl (Sci-fi / Impact / Interface Sounds)",
                "Efectos de sonido — Kenney.nl (Sci-fi / Impact / Interface Sounds)",
                "音效 — Kenney.nl (Sci-fi / Impact / Interface Sounds)",
                "音效 — Kenney.nl (Sci-fi / Impact / Interface Sounds)",
            },
            ["credits.original"] = new[]
            {
                "All terrain, roads, vegetation scatter, UI, and game code are original to this project.",
                "Le terrain, les routes, la végétation, l'interface et le code du jeu sont originaux à ce projet.",
                "地形・道路・植生配置・UI・ゲームコードはすべて本プロジェクトのオリジナルです。",
                "Gelände, Straßen, Vegetation, UI und Spielcode sind Eigenentwicklungen dieses Projekts.",
                "Todo el terreno, caminos, vegetación, UI y código del juego son originales de este proyecto.",
                "全部地形/道路/植被散布/UI/游戏代码为本项目原创。",
                "全部地形/道路/植被散佈/UI/遊戲代碼為本項目原創。",
            },

            // ---- settings panel --------------------------------------------
            ["settings.master_volume"] = new[] { "Master Volume", "Volume principal", "マスター音量", "Gesamtlautstärke", "Volumen general", "主音量", "主音量" },
            ["settings.sfx_volume"] = new[] { "SFX Volume", "Volume des effets", "効果音音量", "Effektlautstärke", "Volumen de efectos", "音效音量", "音效音量" },
            ["settings.engine_volume"] = new[] { "Engine Volume", "Volume du moteur", "エンジン音量", "Motorlautstärke", "Volumen del motor", "引擎音量", "引擎音量" },
            ["settings.mouse_sensitivity"] = new[] { "Mouse Sensitivity", "Sensibilité de la souris", "マウス感度", "Mausempfindlichkeit", "Sensibilidad del ratón", "鼠标灵敏度", "滑鼠靈敏度" },
            ["settings.invert_y"] = new[] { " Invert Y-axis", " Inverser l'axe Y", " Y軸反転", " Y-Achse invertieren", " Invertir eje Y", " 反转Y轴", " 反轉Y軸" },
            ["settings.colorblind"] = new[]
            {
                " Colourblind mode (high-contrast blue/orange)",
                " Mode daltonien (bleu/orange à fort contraste)",
                " 色覚サポートモード（高コントラスト青/橙）",
                " Farbenblind-Modus (kontrastreich Blau/Orange)",
                " Modo daltónico (azul/naranja de alto contraste)",
                " 色盲模式(高对比蓝/橙配色)",
                " 色盲模式(高對比藍/橙配色)",
            },
            ["settings.camera_shake"] = new[] { " Camera Shake", " Tremblement de caméra", " カメラシェイク", " Kameraverwacklung", " Vibración de cámara", " 镜头震动", " 鏡頭震動" },
            ["settings.ui_scale"] = new[] { "UI Scale", "Échelle de l'interface", "UIスケール", "UI-Skalierung", "Escala de la interfaz", "界面缩放", "介面縮放" },
            ["settings.auto_attack"] = new[]
            {
                " Auto-Attack (finishes a committed dive after hard lock)",
                " Attaque automatique (termine un piqué engagé après verrouillage)",
                " 自動攻撃（ハードロック後、突入した降下を自動で完了）",
                " Automatischer Angriff (beendet einen eingeleiteten Sturzflug nach Zielerfassung)",
                " Ataque automático (completa un picado ya iniciado tras el bloqueo)",
                " 自动攻击(锁定并俯冲后无人机自动完成攻击)",
                " 自動攻擊(鎖定並俯衝後無人機自動完成攻擊)",
            },
            ["settings.quality"] = new[] { "Quality", "Qualité", "画質", "Qualität", "Calidad", "画质", "畫質" },
            ["settings.language"] = new[] { "Language", "Langue", "言語", "Sprache", "Idioma", "语言", "語言" },
            ["settings.fullscreen"] = new[] { "Fullscreen", "Plein écran", "フルスクリーン", "Vollbild", "Pantalla completa", "全屏", "全螢幕" },

            // ---- pause menu --------------------------------------------------
            ["pause.title"] = new[] { "Paused", "Pause", "一時停止", "Pausiert", "Pausado", "已暂停", "已暫停" },
            ["pause.resume"] = new[] { "Resume", "Reprendre", "再開", "Fortsetzen", "Reanudar", "继续", "繼續" },
            ["pause.controls"] = new[] { "Controls", "Commandes", "操作方法", "Steuerung", "Controles", "操作说明", "操作說明" },
            ["pause.restart"] = new[] { "Restart", "Recommencer", "リスタート", "Neu starten", "Reiniciar", "重新开始", "重新開始" },
            ["pause.main_menu"] = new[] { "Main Menu", "Menu principal", "メインメニューへ", "Hauptmenü", "Menú principal", "返回主菜单", "返回主選單" },

            // ---- first run tip -------------------------------------------
            ["briefing.title"] = new[] { "Pre-Flight Briefing", "Briefing avant vol", "出撃ブリーフィング", "Einsatzbesprechung", "Briefing previo al vuelo", "出击须知", "出擊須知" },
            ["briefing.launch"] = new[] { "Launch", "Décoller", "出撃開始", "Starten", "Despegar", "开始出击", "開始出擊" },

            // ---- control lines (mouse/keyboard) ---------------------------
            ["ctrl.mkb.aim"] = new[]
            {
                "Mouse movement   aim / steer — the drone flies where you point",
                "Mouvement souris  viser / diriger — le drone vole où vous pointez",
                "マウス移動   照準／操縦 — 無人機は照準方向へ飛びます",
                "Mausbewegung   Zielen / Steuern — die Drohne fliegt, wohin du zeigst",
                "Movimiento del ratón   apuntar / dirigir — el dron vuela hacia donde apuntas",
                "鼠标移动     瞄准 / 转向 —— 无人机朝你指的方向飞",
                "滑鼠移動     瞄準 / 轉向 —— 無人機朝你指的方向飛",
            },
            ["ctrl.mkb.fire"] = new[]
            {
                "Left click       detonate warhead",
                "Clic gauche      faire exploser la charge",
                "左クリック   弾頭起爆",
                "Linksklick       Sprengkopf zünden",
                "Clic izquierdo   detonar la ojiva",
                "左键         引爆战斗部",
                "左鍵         引爆戰鬥部",
            },
            ["ctrl.mkb.precision"] = new[]
            {
                "Right click (hold) precision (zoom in + slow aim)",
                "Clic droit (maintenir) précision (zoom + visée lente)",
                "右クリック（長押し） 精密照準（ズーム＋低速照準）",
                "Rechtsklick (halten) Präzision (Zoom + langsames Zielen)",
                "Clic derecho (mantener) precisión (zoom + puntería lenta)",
                "右键(按住)   精瞄(拉近 + 减速瞄准)",
                "右鍵(按住)   精瞄(拉近 + 減速瞄準)",
            },
            ["ctrl.mkb.throttle"] = new[]
            {
                "W / S     throttle / brake     Shift  boost",
                "W / S     accélérer / freiner     Maj  accélération",
                "W / S   スロットル／ブレーキ    Shift  ブースト",
                "W / S     Gas / Bremse     Umschalt  Boost",
                "W / S     acelerar / frenar     Mayús  impulso",
                "W / S        油门 / 刹车        Shift  加速",
                "W / S        油門 / 剎車        Shift  加速",
            },
            ["ctrl.mkb.trim"] = new[]
            {
                "Space / Ctrl  climb / descend trim",
                "Espace / Ctrl  compensation montée / descente",
                "Space / Ctrl  上昇／下降トリム",
                "Leertaste / Strg  Steig-/Sinktrimmung",
                "Espacio / Ctrl  compensación de ascenso/descenso",
                "空格 / Ctrl  升降微调",
                "空格 / Ctrl  升降微調",
            },
            ["ctrl.mkb.roll"] = new[]
            {
                "Q / E    roll               R    recall",
                "Q / E    roulis             R    rappel",
                "Q / E    ロール             R    リコール",
                "Q / E    Rollen             R    Rückruf",
                "Q / E    alabeo             R    retirada",
                "Q / E        横滚               R      呼回",
                "Q / E        橫滾               R      呼回",
            },
            ["ctrl.mkb.cruise"] = new[]
            {
                "A          toggle auto-cruise",
                "A          basculer pilote auto",
                "A   自動巡航 切替",
                "A          Autopilot umschalten",
                "A          alternar piloto automático",
                "A            切换自动巡航",
                "A            切換自動巡航",
            },
            ["ctrl.pad.cruise"] = new[]
            {
                "X          toggle auto-cruise",
                "X          basculer pilote auto",
                "X   自動巡航 切替",
                "X          Autopilot umschalten",
                "X          alternar piloto automático",
                "X            切换自动巡航",
                "X            切換自動巡航",
            },
            ["hud.cruise_on"] = new[]
            {
                "CRUISE", "PILOTE AUTO", "自動巡航", "AUTOPILOT", "CRUCERO", "自动巡航", "自動巡航",
            },
            ["ctrl.pause"] = new[]
            {
                "Esc       pause / resume",
                "Échap     pause / reprendre",
                "Esc   一時停止／再開",
                "Esc       Pause / Fortsetzen",
                "Esc       pausar / reanudar",
                "Esc          暂停 / 继续",
                "Esc          暫停 / 繼續",
            },

            // ---- control lines (gamepad) -----------------------------------
            ["ctrl.pad.aim"] = new[]
            {
                "Right stick   aim / steer — the drone flies where you point",
                "Stick droit   viser / diriger — le drone vole où vous pointez",
                "右スティック   照準／操縦 — 無人機は照準方向へ飛びます",
                "Rechter Stick   Zielen / Steuern — die Drohne fliegt, wohin du zeigst",
                "Stick derecho   apuntar / dirigir — el dron vuela hacia donde apuntas",
                "右摇杆       瞄准 / 转向 —— 无人机朝你指的方向飞",
                "右搖桿       瞄準 / 轉向 —— 無人機朝你指的方向飛",
            },
            ["ctrl.pad.fire"] = new[]
            {
                "A / RB        detonate warhead",
                "A / RB        faire exploser la charge",
                "A / RB   弾頭起爆",
                "A / RB        Sprengkopf zünden",
                "A / RB        detonar la ojiva",
                "A / RB       引爆战斗部",
                "A / RB       引爆戰鬥部",
            },
            ["ctrl.pad.precision"] = new[]
            {
                "Left trigger (hold) precision (zoom in + slow aim)",
                "Gâchette gauche (maintenir) précision (zoom + visée lente)",
                "左トリガー（長押し） 精密照準（ズーム＋低速照準）",
                "Linker Trigger (halten) Präzision (Zoom + langsames Zielen)",
                "Gatillo izquierdo (mantener) precisión (zoom + puntería lenta)",
                "左扳机(按住) 精瞄(拉近 + 减速瞄准)",
                "左扳機(按住) 精瞄(拉近 + 減速瞄準)",
            },
            ["ctrl.pad.throttle"] = new[]
            {
                "Left stick Y  throttle / brake     Right trigger  boost",
                "Stick gauche Y  accélérer / freiner     Gâchette droite  accélération",
                "左スティックY  スロットル／ブレーキ    右トリガー  ブースト",
                "Linker Stick Y  Gas / Bremse     Rechter Trigger  Boost",
                "Stick izquierdo Y  acelerar / frenar     Gatillo derecho  impulso",
                "左摇杆Y      油门 / 刹车        右扳机  加速",
                "左搖桿Y      油門 / 剎車        右扳機  加速",
            },
            ["ctrl.pad.trim"] = new[]
            {
                "RB / LB       climb / descend trim",
                "RB / LB       compensation montée / descente",
                "RB / LB   上昇／下降トリム",
                "RB / LB       Steig-/Sinktrimmung",
                "RB / LB       compensación de ascenso/descenso",
                "RB / LB      升降微调",
                "RB / LB      升降微調",
            },
            ["ctrl.pad.roll"] = new[]
            {
                "Left stick X  roll               Y     recall",
                "Stick gauche X  roulis           Y     rappel",
                "左スティックX  ロール           Y    リコール",
                "Linker Stick X  Rollen           Y     Rückruf",
                "Stick izquierdo X  alabeo        Y     retirada",
                "左摇杆X      横滚               Y       呼回",
                "左搖桿X      橫滾               Y       呼回",
            },
            ["ctrl.hint_hold_h"] = new[]
            {
                "(hold H for controls)",
                "(maintenir H pour les commandes)",
                "（Hを長押しで操作表示）",
                "(H halten für Steuerung)",
                "(mantén H para ver los controles)",
                "(hold H for controls)",
                "(hold H for controls)",
            },

            // ---- HUD ----------------------------------------------------
            ["hud.column"] = new[]
            {
                "COLUMN   {0}/{1} destroyed", "COLONNE   {0}/{1} détruits", "車列　{0}/{1} 撃破",
                "KOLONNE   {0}/{1} zerstört", "COLUMNA   {0}/{1} destruidos", "COLUMN   {0}/{1} destroyed", "COLUMN   {0}/{1} destroyed",
            },
            ["hud.drones"] = new[]
            {
                "DRONES   {0}   ({1})", "DRONES   {0}   ({1})", "ドローン　{0}　（{1}）",
                "DROHNEN   {0}   ({1})", "DRONES   {0}   ({1})", "DRONES   {0}   ({1})", "DRONES   {0}   ({1})",
            },
            ["hud.bonus"] = new[]
            {
                "BONUS   {0}/{1}", "BONUS   {0}/{1}", "ボーナス　{0}/{1}",
                "BONUS   {0}/{1}", "BONUS   {0}/{1}", "BONUS   {0}/{1}", "BONUS   {0}/{1}",
            },
            ["hud.broke_through"] = new[]
            {
                "BROKE THROUGH   {0}", "PERCÉE   {0}", "突破許　{0}",
                "DURCHBRUCH   {0}", "IRRUPCIÓN   {0}", "BROKE THROUGH   {0}", "BROKE THROUGH   {0}",
            },
            ["hud.alt"] = new[] { "ALT {0} m", "ALT {0} m", "高度 {0} m", "HÖHE {0} m", "ALT {0} m", "ALT {0} m", "ALT {0} m" },
            ["hud.boundary_warning"] = new[]
            {
                "⚠ LEAVING COMBAT AREA",
                "⚠ VOUS QUITTEZ LA ZONE DE COMBAT",
                "⚠ 戦闘区域から離脱中",
                "⚠ KAMPFGEBIET WIRD VERLASSEN",
                "⚠ ABANDONANDO LA ZONA DE COMBATE",
                "⚠ 返回战斗区域 · LEAVING COMBAT AREA",
                "⚠ 返回戰鬥區域 · LEAVING COMBAT AREA",
            },
            ["hud.target_destroyed"] = new[]
            {
                "TARGET DESTROYED  {0}/{1}", "CIBLE DÉTRUITE  {0}/{1}", "目標撃破　{0}/{1}",
                "ZIEL ZERSTÖRT  {0}/{1}", "OBJETIVO DESTRUIDO  {0}/{1}", "TARGET DESTROYED  {0}/{1}", "TARGET DESTROYED  {0}/{1}",
            },
            ["hud.bonus_destroyed"] = new[]
            {
                "BONUS TARGET DESTROYED  {0}/{1}", "CIBLE BONUS DÉTRUITE  {0}/{1}", "ボーナス目標撃破　{0}/{1}",
                "BONUSZIEL ZERSTÖRT  {0}/{1}", "OBJETIVO BONUS DESTRUIDO  {0}/{1}", "BONUS TARGET DESTROYED  {0}/{1}", "BONUS TARGET DESTROYED  {0}/{1}",
            },
            ["hud.lock"] = new[] { "LOCK", "VERROUILLÉ", "ロックオン", "ZIEL ERFASST", "BLOQUEADO", "LOCK", "LOCK" },
            ["hud.auto_attack"] = new[] { "AUTO-ATTACK", "ATTAQUE AUTO", "自動攻撃中", "AUTO-ANGRIFF", "ATAQUE AUTO", "AUTO-ATTACK", "AUTO-ATTACK" },
            ["hud.briefing.line1"] = new[]
            {
                "RECON  ·  enemy armour column advancing up the road",
                "RECON  ·  colonne blindée ennemie progressant sur la route",
                "偵察　・　敵機甲部隊が道路を前進中",
                "AUFKLÄRUNG  ·  feindliche Panzerkolonne rückt auf der Straße vor",
                "RECONOCIMIENTO  ·  columna acorazada enemiga avanza por la carretera",
                "RECON  ·  enemy armour column advancing up the road",
                "RECON  ·  enemy armour column advancing up the road",
            },
            ["hud.briefing.line2"] = new[]
            {
                "Destroy every vehicle before they break through — mind the return fire",
                "Détruisez tous les véhicules avant qu'ils ne percent — attention aux tirs de riposte",
                "全車両を突破前に撃破せよ ― 反撃射撃に注意",
                "Zerstöre alle Fahrzeuge, bevor sie durchbrechen — Vorsicht vor Abwehrfeuer",
                "Destruye todos los vehículos antes de que rompan el cerco — cuidado con el fuego de respuesta",
                "Destroy every vehicle before they break through — mind the return fire",
                "Destroy every vehicle before they break through — mind the return fire",
            },

            // ---- end panel -------------------------------------------------
            ["end.won"] = new[] { "COLUMN DESTROYED", "COLONNE DÉTRUITE", "車列殲滅", "KOLONNE ZERSTÖRT", "COLUMNA DESTRUIDA", "COLUMN DESTROYED", "COLUMN DESTROYED" },
            ["end.lost"] = new[] { "MISSION FAILED", "MISSION ÉCHOUÉE", "作戦失敗", "MISSION FEHLGESCHLAGEN", "MISIÓN FALLIDA", "MISSION FAILED", "MISSION FAILED" },
            ["end.destroyed"] = new[]
            {
                "Destroyed      {0} / {1}", "Détruits       {0} / {1}", "撃破数　　　　{0} / {1}",
                "Zerstört       {0} / {1}", "Destruidos     {0} / {1}", "Destroyed      {0} / {1}", "Destroyed      {0} / {1}",
            },
            ["end.broke_through"] = new[]
            {
                "Broke through  {0}", "Percée         {0}", "突破数　　　　{0}",
                "Durchbruch     {0}", "Irrupción      {0}", "Broke through  {0}", "Broke through  {0}",
            },
            ["end.bonus_targets"] = new[]
            {
                "Bonus targets  {0} / {1}", "Cibles bonus   {0} / {1}", "ボーナス目標　{0} / {1}",
                "Bonusziele     {0} / {1}", "Objetivos bonus {0} / {1}", "Bonus targets  {0} / {1}", "Bonus targets  {0} / {1}",
            },
            ["end.drones_used"] = new[]
            {
                "Drones used    {0} / {1}", "Drones utilisés {0} / {1}", "使用機数　　　{0} / {1}",
                "Drohnen genutzt {0} / {1}", "Drones usados  {0} / {1}", "Drones used    {0} / {1}", "Drones used    {0} / {1}",
            },
            ["end.time"] = new[]
            {
                "Time           {0}:{1}", "Temps          {0}:{1}", "経過時間　　　{0}:{1}",
                "Zeit           {0}:{1}", "Tiempo         {0}:{1}", "Time           {0}:{1}", "Time           {0}:{1}",
            },
            ["end.score_grade"] = new[]
            {
                "SCORE   {0}      GRADE   {1}", "SCORE   {0}      NOTE   {1}", "スコア　{0}　　評価　{1}",
                "PUNKTE   {0}      NOTE   {1}", "PUNTUACIÓN   {0}      NOTA   {1}", "SCORE   {0}      GRADE   {1}", "SCORE   {0}      GRADE   {1}",
            },
            ["end.next_mission"] = new[] { "Next Mission ▶", "Mission suivante ▶", "次のミッション ▶", "Nächste Mission ▶", "Siguiente misión ▶", "Next Mission ▶", "Next Mission ▶" },
            ["end.restart"] = new[] { "Restart", "Recommencer", "リスタート", "Neu starten", "Reiniciar", "Restart", "Restart" },
            ["end.main_menu"] = new[] { "Main Menu", "Menu principal", "メインメニュー", "Hauptmenü", "Menú principal", "Main Menu", "Main Menu" },
            ["end.quit"] = new[] { "Quit", "Quitter", "終了", "Beenden", "Salir", "Quit", "Quit" },

            // ---- vehicle display names (HudController's lock-on label reads
            // these live via Loc.Get(tgt.displayName) — Vehicle.displayName
            // stores the KEY, baked in at build time by IronfieldSetup, not
            // the literal text, so it re-localizes without a rebuild) --------
            ["vehicle.tank"] = new[] { "Main battle tank", "Char de combat principal", "主力戦車", "Kampfpanzer", "Carro de combate principal", "主战坦克", "主戰戰車" },
            ["vehicle.ifv"] = new[] { "Infantry fighting vehicle", "Véhicule de combat d'infanterie", "歩兵戦闘車", "Schützenpanzer", "Vehículo de combate de infantería", "步兵战车", "步兵戰車" },
            ["vehicle.truck"] = new[] { "Supply truck", "Camion de ravitaillement", "補給トラック", "Versorgungs-LKW", "Camión de suministro", "补给卡车", "補給卡車" },
            ["vehicle.spaag"] = new[] { "Self-propelled AA gun", "Canon antiaérien automoteur", "自走対空砲", "Selbstfahrende Flugabwehrkanone", "Cañón antiaéreo autopropulsado", "自行高炮", "自走高砲" },
            ["vehicle.hq"] = new[] { "HQ COMMAND VEHICLE", "VÉHICULE DE COMMANDEMENT", "指揮車両", "GEFECHTSSTANDFAHRZEUG", "VEHÍCULO DE MANDO", "HQ COMMAND VEHICLE", "HQ COMMAND VEHICLE" },
            ["vehicle.outpost"] = new[] { "Recon relay outpost", "Avant-poste relais de reconnaissance", "偵察中継拠点", "Aufklärungs-Relaisposten", "Puesto de enlace de reconocimiento", "侦察中继站", "偵察中繼站" },
        };
    }
}
