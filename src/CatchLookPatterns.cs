#if RAVEN_ITEM
namespace HuginWhispers
{
    // A fish caught in a dive (WorldVisit «Fish») and a new thing looked over (NewGear). Added once (Patterns.Revision 99).
    //   world_fish         it brings you the fish it caught ({thing}: the fish, small letters)
    //   world_fish_eaten   Munin, hungry, ate it there ({thing})
    //   gear_look_armor    a piece of armour you never wore before ({thing}: its name)
    //   gear_look_weapon   a weapon or a shield never held before ({thing})
    internal static class CatchLookPatterns
    {
        internal static readonly string[] Hugin =
        {
            "# --- риба з пірнання, огляд обнови (rev 99)",
            "[world_fish] {хулов_несу}",
            "[gear_look_armor] {хобн_броня}",
            "[gear_look_weapon] {хобн_зброя}",
            "@хулов_несу",
            "Тримай: {thing}. Свіжа, з-під самої хвилі.",
            "Пірнув, виринув, приніс. Мокрий, але гордий.",
            "{thing}. Риба сама в дзьоб попросилася.",
            "Я тепер ще й рибалка. Це тобі.",
            "@хобн_броня",
            "Нове вбрання? Хм. Тобі личить. Майже.",
            "{thing}. Розумний вибір. Нарешті.",
            "Дай подивлюся... Так, у цьому тебе важче вбити. Мені подобається.",
            "Обнова! Тепер ти схож{ий|а} на того, хто знає, що робить.",
            "@хобн_зброя",
            "{thing}. Гостре. Тримай гострим кінцем від мене.",
            "Нова зброя — нові помилки. Але гарна.",
            "Хм, {thing}. Ворогам це не сподобається. Мені — так.",
        };

        internal static readonly string[] Munin =
        {
            "# --- риба з пірнання, огляд обнови (rev 99)",
            "[world_fish] {мулов_несу}",
            "[world_fish_eaten] {мулов_зїв}",
            "[gear_look_armor] {мобн_броня}",
            "[gear_look_weapon] {мобн_зброя}",
            "@мулов_несу",
            "На, {thing}. Я ситий, а то б не віддав.",
            "Тримай рибину. Мокрий, як щур, але з уловом.",
            "Пірнув за нею. Холодно, бля. Цінуй.",
            "@мулов_зїв",
            "Хрум. Була риба — нема риби. Мені нужніше.",
            "Шо? Я голодний був. Наступна — твоя. Може.",
            "Свіжа рибка. Ти б однаково не встиг{|ла}.",
            "@мобн_броня",
            "Шо це на тобі? {thing}? Ну... хоч не в лахмітті тепер.",
            "Обнова, бля! Тільки не плач, коли подряпають.",
            "Сидить криво. Але сидить. Сойде.",
            "О, приодяг{ся|лась}! На свято зібра{вся|лась} чи на смерть?",
            "@мобн_зброя",
            "{thing}? Ну-ну. Подивимось, кого ти тим зарубаєш.",
            "Нова цяцька. Тільки не махай біля мене.",
            "Ого, яка штука. Дай дзьобну. Ні? Жмот.",
        };
    }
}
#endif
