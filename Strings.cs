using System.Collections.Generic;

namespace UnsteadyBody
{
    /// <summary>
    /// Every piece of text a player sees, in each language Hollow Knight ships with.
    /// Picked by the game's current language; anything missing falls back to English.
    ///
    /// To correct or add a translation, change the entry for that language below.
    /// Language codes are the game's: EN, FR, DE, ES, IT, PT (Brazilian), RU, ZH
    /// (Simplified Chinese), JA, KO.
    ///
    /// Notes for translators:
    /// - The two hints are added straight onto the end of the charm's description,
    ///   so in languages that put a space between sentences they start with one.
    /// - "Stabilize"/"Destabilize" sit beside the Dream Nail button in the charm menu,
    ///   like the game's own "Equip"/"Unequip", so keep them short.
    /// - The hints stay deliberately vague, like Kingsoul's: they shouldn't say how
    ///   to turn the charm over.
    /// </summary>
    internal static class Strings
    {
        internal static string CharmName => Get("CharmName");
        internal static string CharmDescription => Get("CharmDescription");
        internal static string SteadyHint => Get("SteadyHint");
        internal static string UnsteadyHint => Get("UnsteadyHint");
        internal static string Stabilize => Get("Stabilize");
        internal static string Destabilize => Get("Destabilize");

        // The mod's options menu stays in English on purpose, like the Modding API's own
        // menus around it: the API builds it once at startup in its own font, which
        // can't show Chinese, Japanese or Korean. Its translations are kept below
        // in case that changes.
        internal static string Menu(string id) => Table["EN"].TryGetValue(id, out string text) ? text : id;
        internal static string[] OffOn => new[] { Menu("Off"), Menu("On") };
        internal static string[] StrengthNames => new[] { Menu("Wobbly"), Menu("Comical"), Menu("Absurd"), Menu("Ludicrous") };
        internal static string[] SpeedLimitNames => new[] { Menu("SpeedOn"), Menu("SpeedOff2x"), Menu("SpeedOff4x") };

        internal static string Get(string id)
        {
            string lang = CurrentLanguage();
            if (Table.TryGetValue(lang, out var strings) && strings.TryGetValue(id, out string text)) return text;
            return Table["EN"].TryGetValue(id, out string english) ? english : id;
        }

        private static string _loggedLanguage;

        internal static string CurrentLanguage()
        {
            string code;
            try { code = global::Language.Language.CurrentLanguage().ToString().ToUpperInvariant(); }
            catch { code = "EN"; }
            if (code != _loggedLanguage)
            {
                _loggedLanguage = code;
                UnsteadyBodyMod.Verbose(Table.ContainsKey(code)
                    ? $"Text language: {code}"
                    : $"Text language: {code} (no translation yet, using English)");
            }
            return code;
        }

        private static readonly Dictionary<string, Dictionary<string, string>> Table =
            new Dictionary<string, Dictionary<string, string>>
        {
            ["EN"] = new Dictionary<string, string>
            {
                ["CharmName"] = "Unsteady Body",
                ["CharmDescription"] =
                    "Hurls its bearer backwards whenever they strike an enemy with a nail, " +
                    "and skywards when they strike from above.\n\n" +
                    "Allows one to see much more of Hallownest, much more quickly.",
                ["SteadyHint"] = " Some say it does the very opposite when turned over.",
                ["UnsteadyHint"] = " Turn it back over to regain one's footing.",
                ["Stabilize"] = "Stabilize",
                ["Destabilize"] = "Destabilize",

                ["Off"] = "Off",
                ["On"] = "On",
                ["Wobbly"] = "Wobbly",
                ["Comical"] = "Comical",
                ["Absurd"] = "Absurd",
                ["Ludicrous"] = "Ludicrous",
                ["SpeedOn"] = "On",
                ["SpeedOff2x"] = "Off: 2x faster",
                ["SpeedOff4x"] = "Off: 4x faster",

                ["StrengthName"] = "Launch strength",
                ["StrengthDesc"] = "How far a nail strike sends them.",
                ["SpeedName"] = "Speed limit",
                ["SpeedDesc"] = "Same distance, faster. Off may let them pass through walls.",
                ["PogoName"] = "Pogo launch",
                ["PogoDesc"] = "Striking downwards sends them skywards.",
                ["SlamName"] = "Upward slam",
                ["SlamDesc"] = "Striking upwards in the air drops them like a stone.",
                ["CarryName"] = "Carry momentum",
                ["CarryDesc"] = "Experimental: an unfinished launch continues into the next room.",
                ["SoundsName"] = "Sounds",
                ["SoundsDesc"] = "A whoosh on launch, a thud on impact, and anything else it has to say.",
                ["TrackerName"] = "Tracker bubble",
                ["TrackerDesc"] = "Shows where they are when they fly off screen.",
            },

            ["FR"] = new Dictionary<string, string>
            {
                ["CharmName"] = "Corps instable",
                ["CharmDescription"] =
                    "Projette son porteur en arrière chaque fois qu'il frappe un ennemi avec son aiguillon, " +
                    "et vers le ciel lorsqu'il frappe d'en haut.\n\n" +
                    "Permet de voir bien plus d'Hallownest, bien plus vite.",
                ["SteadyHint"] = " On dit qu'une fois retourné, il fait tout le contraire.",
                ["UnsteadyHint"] = " Retournez-le à nouveau pour retrouver l'équilibre.",
                ["Stabilize"] = "Stabiliser",
                ["Destabilize"] = "Déstabiliser",

                ["Off"] = "Désactivé",
                ["On"] = "Activé",
                ["Wobbly"] = "Chancelant",
                ["Comical"] = "Comique",
                ["Absurd"] = "Absurde",
                ["Ludicrous"] = "Grotesque",
                ["SpeedOn"] = "Activée",
                ["SpeedOff2x"] = "Désactivée : 2x plus vite",
                ["SpeedOff4x"] = "Désactivée : 4x plus vite",

                ["StrengthName"] = "Force de projection",
                ["StrengthDesc"] = "Jusqu'où l'envoie un coup d'aiguillon.",
                ["SpeedName"] = "Limite de vitesse",
                ["SpeedDesc"] = "Même distance, plus vite. Sans limite, il peut traverser les murs.",
                ["PogoName"] = "Rebond propulsé",
                ["PogoDesc"] = "Frapper vers le bas l'envoie vers le ciel.",
                ["SlamName"] = "Chute fracassante",
                ["SlamDesc"] = "Frapper vers le haut en l'air le fait tomber comme une pierre.",
                ["CarryName"] = "Conserver l'élan",
                ["CarryDesc"] = "Expérimental : une projection inachevée continue dans la salle suivante.",
                ["SoundsName"] = "Sons",
                ["SoundsDesc"] = "Un souffle au départ, un choc à l'impact, et tout ce qu'il a d'autre à dire.",
                ["TrackerName"] = "Bulle de suivi",
                ["TrackerDesc"] = "Montre où il se trouve quand il sort de l'écran.",
            },

            ["DE"] = new Dictionary<string, string>
            {
                ["CharmName"] = "Wankender Körper",
                ["CharmDescription"] =
                    "Schleudert seinen Träger zurück, wann immer er einen Feind mit dem Nagel trifft, " +
                    "und himmelwärts, wenn er von oben zuschlägt.\n\n" +
                    "Ermöglicht es, viel mehr von Hallownest zu sehen, und das viel schneller.",
                ["SteadyHint"] = " Manche sagen, umgedreht bewirkt es genau das Gegenteil.",
                ["UnsteadyHint"] = " Dreh es wieder um, um festen Stand zurückzugewinnen.",
                ["Stabilize"] = "Stabilisieren",
                ["Destabilize"] = "Destabilisieren",

                ["Off"] = "Aus",
                ["On"] = "An",
                ["Wobbly"] = "Wackelig",
                ["Comical"] = "Komisch",
                ["Absurd"] = "Absurd",
                ["Ludicrous"] = "Aberwitzig",
                ["SpeedOn"] = "An",
                ["SpeedOff2x"] = "Aus: 2x schneller",
                ["SpeedOff4x"] = "Aus: 4x schneller",

                ["StrengthName"] = "Wurfstärke",
                ["StrengthDesc"] = "Wie weit ihn ein Nagelschlag schleudert.",
                ["SpeedName"] = "Tempolimit",
                ["SpeedDesc"] = "Gleiche Strecke, schneller. Ohne Limit kann er durch Wände gleiten.",
                ["PogoName"] = "Pogo-Start",
                ["PogoDesc"] = "Ein Schlag nach unten schleudert ihn himmelwärts.",
                ["SlamName"] = "Aufwärts-Sturz",
                ["SlamDesc"] = "Ein Schlag nach oben in der Luft lässt ihn wie einen Stein fallen.",
                ["CarryName"] = "Schwung mitnehmen",
                ["CarryDesc"] = "Experimentell: Ein unvollendeter Flug geht im nächsten Raum weiter.",
                ["SoundsName"] = "Geräusche",
                ["SoundsDesc"] = "Ein Rauschen beim Abheben, ein Aufprall beim Landen und alles, was es sonst noch zu sagen hat.",
                ["TrackerName"] = "Ortungsblase",
                ["TrackerDesc"] = "Zeigt, wo er ist, wenn er aus dem Bild fliegt.",
            },

            ["ES"] = new Dictionary<string, string>
            {
                ["CharmName"] = "Cuerpo inestable",
                ["CharmDescription"] =
                    "Lanza a su portador hacia atrás cada vez que golpea a un enemigo con el aguijón, " +
                    "y hacia el cielo cuando golpea desde arriba.\n\n" +
                    "Permite ver mucho más de Hallownest, mucho más rápido.",
                ["SteadyHint"] = " Dicen que, si se le da la vuelta, hace justo lo contrario.",
                ["UnsteadyHint"] = " Dale la vuelta de nuevo para recuperar el equilibrio.",
                ["Stabilize"] = "Estabilizar",
                ["Destabilize"] = "Desestabilizar",

                ["Off"] = "No",
                ["On"] = "Sí",
                ["Wobbly"] = "Tambaleante",
                ["Comical"] = "Cómico",
                ["Absurd"] = "Absurdo",
                ["Ludicrous"] = "Disparatado",
                ["SpeedOn"] = "Sí",
                ["SpeedOff2x"] = "No: 2x más rápido",
                ["SpeedOff4x"] = "No: 4x más rápido",

                ["StrengthName"] = "Fuerza de lanzamiento",
                ["StrengthDesc"] = "Lo lejos que lo envía un golpe de aguijón.",
                ["SpeedName"] = "Límite de velocidad",
                ["SpeedDesc"] = "Misma distancia, más rápido. Sin límite, podría atravesar paredes.",
                ["PogoName"] = "Lanzamiento rebote",
                ["PogoDesc"] = "Golpear hacia abajo lo lanza hacia el cielo.",
                ["SlamName"] = "Caída en picado",
                ["SlamDesc"] = "Golpear hacia arriba en el aire lo hace caer como una piedra.",
                ["CarryName"] = "Conservar impulso",
                ["CarryDesc"] = "Experimental: un lanzamiento sin terminar continúa en la siguiente sala.",
                ["SoundsName"] = "Sonidos",
                ["SoundsDesc"] = "Un silbido al salir, un golpe al chocar y todo lo demás que tenga que decir.",
                ["TrackerName"] = "Burbuja de rastreo",
                ["TrackerDesc"] = "Muestra dónde está cuando sale volando de la pantalla.",
            },

            ["IT"] = new Dictionary<string, string>
            {
                ["CharmName"] = "Corpo instabile",
                ["CharmDescription"] =
                    "Scaglia all'indietro chi lo indossa ogni volta che colpisce un nemico con l'aculeo, " +
                    "e verso il cielo quando colpisce dall'alto.\n\n" +
                    "Permette di vedere molto più di Hallownest, molto più in fretta.",
                ["SteadyHint"] = " Si dice che, capovolto, faccia esattamente il contrario.",
                ["UnsteadyHint"] = " Giralo di nuovo per ritrovare l'equilibrio.",
                ["Stabilize"] = "Stabilizza",
                ["Destabilize"] = "Destabilizza",

                ["Off"] = "No",
                ["On"] = "Sì",
                ["Wobbly"] = "Traballante",
                ["Comical"] = "Comico",
                ["Absurd"] = "Assurdo",
                ["Ludicrous"] = "Folle",
                ["SpeedOn"] = "Sì",
                ["SpeedOff2x"] = "No: 2x più veloce",
                ["SpeedOff4x"] = "No: 4x più veloce",

                ["StrengthName"] = "Forza del lancio",
                ["StrengthDesc"] = "Quanto lontano lo manda un colpo d'aculeo.",
                ["SpeedName"] = "Limite di velocità",
                ["SpeedDesc"] = "Stessa distanza, più in fretta. Senza limite potrebbe attraversare i muri.",
                ["PogoName"] = "Lancio a rimbalzo",
                ["PogoDesc"] = "Colpire verso il basso lo scaglia verso il cielo.",
                ["SlamName"] = "Schianto verso l'alto",
                ["SlamDesc"] = "Colpire verso l'alto in aria lo fa cadere come un sasso.",
                ["CarryName"] = "Mantieni lo slancio",
                ["CarryDesc"] = "Sperimentale: un lancio non concluso prosegue nella stanza successiva.",
                ["SoundsName"] = "Suoni",
                ["SoundsDesc"] = "Un fruscio al lancio, un tonfo all'impatto e tutto il resto che ha da dire.",
                ["TrackerName"] = "Bolla di tracciamento",
                ["TrackerDesc"] = "Mostra dov'è quando vola fuori dallo schermo.",
            },

            ["PT"] = new Dictionary<string, string>
            {
                ["CharmName"] = "Corpo Instável",
                ["CharmDescription"] =
                    "Arremessa seu portador para trás sempre que ele atinge um inimigo com o ferrão, " +
                    "e para o céu quando ataca de cima.\n\n" +
                    "Permite ver muito mais de Hallownest, muito mais rápido.",
                ["SteadyHint"] = " Dizem que, quando virado, faz exatamente o oposto.",
                ["UnsteadyHint"] = " Vire-o de volta para recuperar o equilíbrio.",
                ["Stabilize"] = "Estabilizar",
                ["Destabilize"] = "Desestabilizar",

                ["Off"] = "Desligado",
                ["On"] = "Ligado",
                ["Wobbly"] = "Bambo",
                ["Comical"] = "Cômico",
                ["Absurd"] = "Absurdo",
                ["Ludicrous"] = "Insano",
                ["SpeedOn"] = "Ligado",
                ["SpeedOff2x"] = "Desligado: 2x mais rápido",
                ["SpeedOff4x"] = "Desligado: 4x mais rápido",

                ["StrengthName"] = "Força do arremesso",
                ["StrengthDesc"] = "Até onde um golpe de ferrão o arremessa.",
                ["SpeedName"] = "Limite de velocidade",
                ["SpeedDesc"] = "Mesma distância, mais rápido. Sem limite, ele pode atravessar paredes.",
                ["PogoName"] = "Arremesso de pogo",
                ["PogoDesc"] = "Golpear para baixo o arremessa para o céu.",
                ["SlamName"] = "Queda em pique",
                ["SlamDesc"] = "Golpear para cima no ar o faz cair como uma pedra.",
                ["CarryName"] = "Manter impulso",
                ["CarryDesc"] = "Experimental: um arremesso inacabado continua na sala seguinte.",
                ["SoundsName"] = "Sons",
                ["SoundsDesc"] = "Um zunido no arremesso, um baque no impacto e tudo mais que ele tiver a dizer.",
                ["TrackerName"] = "Bolha rastreadora",
                ["TrackerDesc"] = "Mostra onde ele está quando voa para fora da tela.",
            },

            ["RU"] = new Dictionary<string, string>
            {
                ["CharmName"] = "Неустойчивое тело",
                ["CharmDescription"] =
                    "Отбрасывает владельца назад всякий раз, когда он бьёт врага гвоздём, " +
                    "и в небо, когда он бьёт сверху.\n\n" +
                    "Позволяет увидеть гораздо больше Халлоунеста — и гораздо быстрее.",
                ["SteadyHint"] = " Говорят, если его перевернуть, он делает ровно обратное.",
                ["UnsteadyHint"] = " Переверните его обратно, чтобы вновь обрести опору.",
                ["Stabilize"] = "Стабилизировать",
                ["Destabilize"] = "Дестабилизировать",

                ["Off"] = "Выкл.",
                ["On"] = "Вкл.",
                ["Wobbly"] = "Шатко",
                ["Comical"] = "Комично",
                ["Absurd"] = "Абсурдно",
                ["Ludicrous"] = "Безумно",
                ["SpeedOn"] = "Вкл.",
                ["SpeedOff2x"] = "Выкл.: в 2 раза быстрее",
                ["SpeedOff4x"] = "Выкл.: в 4 раза быстрее",

                ["StrengthName"] = "Сила броска",
                ["StrengthDesc"] = "Как далеко его отбрасывает удар гвоздём.",
                ["SpeedName"] = "Ограничение скорости",
                ["SpeedDesc"] = "То же расстояние, но быстрее. Без ограничения он может проходить сквозь стены.",
                ["PogoName"] = "Бросок отскоком",
                ["PogoDesc"] = "Удар вниз подбрасывает его в небо.",
                ["SlamName"] = "Удар вверх",
                ["SlamDesc"] = "Удар вверх в воздухе роняет его камнем вниз.",
                ["CarryName"] = "Сохранять инерцию",
                ["CarryDesc"] = "Экспериментально: незаконченный полёт продолжается в следующей комнате.",
                ["SoundsName"] = "Звуки",
                ["SoundsDesc"] = "Свист при броске, глухой удар при столкновении и всё остальное, что ему есть сказать.",
                ["TrackerName"] = "Пузырь слежения",
                ["TrackerDesc"] = "Показывает, где он, когда улетает за пределы экрана.",
            },

            ["ZH"] = new Dictionary<string, string>
            {
                ["CharmName"] = "不稳之躯",
                ["CharmDescription"] =
                    "每当佩戴者用骨钉击中敌人时，都会被向后抛飞；从上方攻击时，则会被抛向天空。\n\n" +
                    "让人能以快得多的速度，看到圣巢更多的角落。",
                ["SteadyHint"] = "据说将它翻过来，效果会恰恰相反。",
                ["UnsteadyHint"] = "将它翻回来，便能重新站稳脚跟。",
                ["Stabilize"] = "稳固",
                ["Destabilize"] = "动摇",

                ["Off"] = "关",
                ["On"] = "开",
                ["Wobbly"] = "摇晃",
                ["Comical"] = "滑稽",
                ["Absurd"] = "荒诞",
                ["Ludicrous"] = "离谱",
                ["SpeedOn"] = "开",
                ["SpeedOff2x"] = "关：快2倍",
                ["SpeedOff4x"] = "关：快4倍",

                ["StrengthName"] = "抛飞力度",
                ["StrengthDesc"] = "一次骨钉攻击能把他抛出多远。",
                ["SpeedName"] = "速度上限",
                ["SpeedDesc"] = "距离不变，速度更快。关闭后他可能会穿墙而过。",
                ["PogoName"] = "下劈弹射",
                ["PogoDesc"] = "向下攻击会把他抛向天空。",
                ["SlamName"] = "上挑坠落",
                ["SlamDesc"] = "在空中向上攻击会让他像石头一样坠落。",
                ["CarryName"] = "保留动量",
                ["CarryDesc"] = "实验性：未结束的抛飞会延续到下一个房间。",
                ["SoundsName"] = "音效",
                ["SoundsDesc"] = "抛飞时的呼啸、撞击时的闷响，以及它想说的其他一切。",
                ["TrackerName"] = "追踪气泡",
                ["TrackerDesc"] = "当他飞出屏幕时显示他的位置。",
            },

            ["JA"] = new Dictionary<string, string>
            {
                ["CharmName"] = "不安定な体",
                ["CharmDescription"] =
                    "釘で敵を打つたびに装着者を後方へ吹き飛ばし、上から打てば空高く打ち上げる。\n\n" +
                    "ハロウネストのより多くを、ずっと速く見て回れる。",
                ["SteadyHint"] = "裏返すと正反対の働きをする、と言う者もいる。",
                ["UnsteadyHint"] = "再び裏返せば、足元を取り戻せる。",
                ["Stabilize"] = "安定化",
                ["Destabilize"] = "不安定化",

                ["Off"] = "オフ",
                ["On"] = "オン",
                ["Wobbly"] = "ぐらぐら",
                ["Comical"] = "コミカル",
                ["Absurd"] = "でたらめ",
                ["Ludicrous"] = "めちゃくちゃ",
                ["SpeedOn"] = "オン",
                ["SpeedOff2x"] = "オフ：2倍速",
                ["SpeedOff4x"] = "オフ：4倍速",

                ["StrengthName"] = "吹き飛びの強さ",
                ["StrengthDesc"] = "釘の一撃でどこまで飛ばされるか。",
                ["SpeedName"] = "速度制限",
                ["SpeedDesc"] = "距離はそのまま、より速く。オフにすると壁をすり抜けることがある。",
                ["PogoName"] = "ホッピング発射",
                ["PogoDesc"] = "下向きに打つと空高く打ち上げられる。",
                ["SlamName"] = "上打ち急降下",
                ["SlamDesc"] = "空中で上向きに打つと石のように落ちる。",
                ["CarryName"] = "勢いを持ち越す",
                ["CarryDesc"] = "実験的：途中の吹き飛びが次の部屋でも続く。",
                ["SoundsName"] = "効果音",
                ["SoundsDesc"] = "発射時の風切り音、衝突時の鈍い音、そのほか言いたいことすべて。",
                ["TrackerName"] = "追跡バブル",
                ["TrackerDesc"] = "画面外へ飛んでいったとき、居場所を表示する。",
            },

            ["KO"] = new Dictionary<string, string>
            {
                ["CharmName"] = "불안정한 몸",
                ["CharmDescription"] =
                    "못으로 적을 칠 때마다 착용자를 뒤로 날려 보내고, 위에서 내려칠 때는 하늘로 날려 보낸다.\n\n" +
                    "할로우네스트를 훨씬 더 많이, 훨씬 더 빠르게 둘러볼 수 있다.",
                ["SteadyHint"] = " 뒤집으면 정반대의 효과를 낸다는 말도 있다.",
                ["UnsteadyHint"] = " 다시 뒤집으면 발 디딜 곳을 되찾을 수 있다.",
                ["Stabilize"] = "안정화",
                ["Destabilize"] = "불안정화",

                ["Off"] = "끔",
                ["On"] = "켬",
                ["Wobbly"] = "흔들흔들",
                ["Comical"] = "우스꽝스러움",
                ["Absurd"] = "터무니없음",
                ["Ludicrous"] = "황당무계",
                ["SpeedOn"] = "켬",
                ["SpeedOff2x"] = "끔: 2배 빠르게",
                ["SpeedOff4x"] = "끔: 4배 빠르게",

                ["StrengthName"] = "날아가는 힘",
                ["StrengthDesc"] = "못 공격 한 번에 얼마나 멀리 날아가는지.",
                ["SpeedName"] = "속도 제한",
                ["SpeedDesc"] = "거리는 같고 더 빠르게. 끄면 벽을 통과할 수도 있다.",
                ["PogoName"] = "포고 발사",
                ["PogoDesc"] = "아래로 공격하면 하늘로 날아간다.",
                ["SlamName"] = "상단 낙하",
                ["SlamDesc"] = "공중에서 위로 공격하면 돌처럼 떨어진다.",
                ["CarryName"] = "가속 유지",
                ["CarryDesc"] = "실험적: 끝나지 않은 비행이 다음 방에서도 이어진다.",
                ["SoundsName"] = "효과음",
                ["SoundsDesc"] = "발사할 때 휙, 부딪힐 때 쿵, 그리고 그 밖에 할 말 전부.",
                ["TrackerName"] = "추적 말풍선",
                ["TrackerDesc"] = "화면 밖으로 날아갔을 때 위치를 보여 준다.",
            },
        };
    }
}
