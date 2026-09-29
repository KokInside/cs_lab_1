using Microsoft.VisualStudio.TestTools.UnitTesting;
using csWinFkey;

namespace csWinFkey.Tests
{
    /// <summary>
    /// Модульные тесты класса FioMask: проверка шаблона, форматирование ввода
    /// «на лету», продолжение ввода после исправлений (Backspace).
    /// </summary>
    [TestClass]
    public class FioMaskTests
    {
        /// <summary>
        /// Имитирует посимвольный ввод строки keys в пустое поле по шаблону mask
        /// (так же, как это делает обработчик KeyPress формы).
        /// Отклонённые символы в строку не попадают.
        /// </summary>
        private static string Type(string mask, string keys, out bool completed)
        {
            var fio = new FioMask(mask);
            string text = "";
            completed = false;
            foreach (char c in keys)
            {
                string? add = fio.Apply(text, c, out bool done);
                if (add != null)
                {
                    text += add;
                    completed = done;
                }
            }
            return text;
        }

        // ---------- 1. Проверка шаблона ----------

        [DataTestMethod]
        [DataRow("Ф и.о.", DisplayName = "Фамилия и инициалы")]
        [DataRow("и.о. Ф", DisplayName = "Инициалы перед фамилией")]
        [DataRow("Ф И О", DisplayName = "Полное ФИО")]
        [DataRow("Ф, И о.", DisplayName = "Запятая-разделитель")]
        [DataRow("(Ф) и.о.", DisplayName = "Ведущий разделитель")]
        [DataRow("иоФ", DisplayName = "Инициалы вплотную")]
        [DataRow("Ф-и.о.", DisplayName = "Дефис-разделитель")]
        public void CheckTemplate_Valid(string mask)
        {
            Assert.IsTrue(FioMask.CheckTemplate(mask));
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Пустой шаблон")]
        [DataRow("ФИО", DisplayName = "Слова вплотную")]
        [DataRow("Фи.о.", DisplayName = "После слова нет разделителя")]
        [DataRow("Ф и.", DisplayName = "Нет поля О")]
        [DataRow("Ф Ф и.о.", DisplayName = "Поле Ф дважды")]
        [DataRow("Ф и.о.х", DisplayName = "Посторонняя буква")]
        [DataRow("F i.o.", DisplayName = "Латиница")]
        public void CheckTemplate_Invalid(string mask)
        {
            Assert.IsFalse(FioMask.CheckTemplate(mask));
        }

        [TestMethod]
        public void Source_ReturnsOriginalMask()
        {
            Assert.AreEqual("Ф и.о.", new FioMask("Ф и.о.").Source);
        }

        // ---------- 2. Форматирование ввода ----------

        [DataTestMethod]
        [DataRow("Ф и.о.", "иванов ии", "Иванов И.И.", DisplayName = "Базовый формат")]
        [DataRow("Ф и.о.", "ИВАНОВ ИИ", "Иванов И.И.", DisplayName = "Регистр игнорируется")]
        [DataRow("Ф и.о.", "новиков-прибой ас", "Новиков-Прибой А.С.", DisplayName = "Двойная фамилия")]
        [DataRow("Ф И О", "новиков алексей силыч ", "Новиков Алексей Силыч", DisplayName = "Полное ФИО")]
        [DataRow("и.о. Ф", "ас новиков ", "А.С. Новиков", DisplayName = "Инициалы перед фамилией")]
        [DataRow("Ф, И о.", "новиков алексей с", "Новиков, Алексей С.", DisplayName = "Запятая дописывается сама")]
        [DataRow("Ф, И о.", "петров,иван и", "Петров, Иван И.", DisplayName = "Разделитель своей клавишей")]
        [DataRow("(Ф) и.о.", "иванов ии", "(Иванов) И.И.", DisplayName = "Ведущий разделитель")]
        [DataRow("Ф-и.о.", "новиков-ас", "Новиков-А.С.", DisplayName = "Дефис закрывает слово")]
        [DataRow("иоФ ", "аспетров ", "АСПетров ", DisplayName = "Инициалы без разделителей")]
        public void Input_CompleteEntry(string mask, string keys, string expected)
        {
            string text = Type(mask, keys, out bool completed);
            Assert.AreEqual(expected, text);
            Assert.IsTrue(completed, "ФИО должна считаться введённой полностью");
        }

        [TestMethod]
        public void Input_LastWord_CompletedOnlyBySpaceOrEnter()
        {
            Assert.AreEqual("И.И. Иванов", Type("и.о. Ф", "ии иванов", out bool done1));
            Assert.IsFalse(done1, "без пробела слово ещё не закончено");

            Type("и.о. Ф", "ии иванов ", out bool done2);
            Assert.IsTrue(done2, "пробел завершает последнее слово");

            Type("и.о. Ф", "ии иванов\r", out bool done3);
            Assert.IsTrue(done3, "Enter завершает последнее слово");
        }

        // ---------- 3. Отклонение недопустимых символов ----------

        [DataTestMethod]
        [DataRow("  иванов   и и", "Иванов И.И.", DisplayName = "Лишние пробелы")]
        [DataRow("ив1ан!ов и2и", "Иванов И.И.", DisplayName = "Цифры и знаки")]
        [DataRow("-иванов ии", "Иванов И.И.", DisplayName = "Дефис в начале слова")]
        [DataRow("новиков--прибой ас", "Новиков-Прибой А.С.", DisplayName = "Два дефиса подряд")]
        [DataRow("новиков- прибой ас", "Новиков-Прибой А.С.", DisplayName = "Пробел после дефиса")]
        [DataRow("иванов -ии", "Иванов И.И.", DisplayName = "Дефис вместо инициала")]
        public void Input_InvalidKeysIgnored(string keys, string expected)
        {
            Assert.AreEqual(expected, Type("Ф и.о.", keys, out _));
        }

        [TestMethod]
        public void Input_SecondHyphenRejected()
        {
            Assert.AreEqual("А-Бв", Type("Ф и.о.", "а-б-в", out bool done));
            Assert.IsFalse(done);
        }

        [TestMethod]
        public void Input_AfterCompletion_Rejected()
        {
            var fio = new FioMask("Ф и.о.");
            Assert.IsNull(fio.Apply("Иванов И.И.", 'а', out _));
        }

        // ---------- 4. Продолжение ввода после исправлений ----------

        [TestMethod]
        public void Continue_AfterDeletedDot_DotRestored()
        {
            // Пользователь стёр точку после первого инициала
            var fio = new FioMask("Ф и.о.");
            Assert.AreEqual(".П.", fio.Apply("Иванов И", 'п', out bool done));
            Assert.IsTrue(done);
        }

        [TestMethod]
        public void Continue_AfterDeletedInitial()
        {
            var fio = new FioMask("Ф и.о.");
            Assert.AreEqual("П.", fio.Apply("Иванов ", 'п', out bool done));
            Assert.IsFalse(done);
        }

        [TestMethod]
        public void Continue_WordAfterDeletedSeparator()
        {
            // Стёрт пробел после фамилии — ввод букв продолжает фамилию
            var fio = new FioMask("Ф и.о.");
            Assert.AreEqual("а", fio.Apply("Иванов", 'А', out _));
            Assert.AreEqual(" ", fio.Apply("Иванов", ' ', out _));
        }

        [TestMethod]
        public void Apply_TextNotMatchingMask_ReturnsNull()
        {
            var fio = new FioMask("Ф и.о.");
            Assert.IsNull(fio.Apply("Иванов, И", 'п', out _));
        }

        // ---------- 5. Разбор готового текста ----------

        [DataTestMethod]
        [DataRow("", true, false, DisplayName = "Пустая строка")]
        [DataRow("Новиков-При", true, false, DisplayName = "Незаконченная фамилия")]
        [DataRow("Иванов И.", true, false, DisplayName = "Один инициал")]
        [DataRow("Иванов И.И.", true, true, DisplayName = "Полная ФИО")]
        [DataRow("Иванов И.И.И", false, false, DisplayName = "Лишний символ")]
        [DataRow("Иванов, И.И.", false, false, DisplayName = "Чужой разделитель")]
        [DataRow("Иванов- И.И.", false, false, DisplayName = "Слово оканчивается дефисом")]
        public void MatchesAndIsComplete(string text, bool matches, bool complete)
        {
            var fio = new FioMask("Ф и.о.");
            Assert.AreEqual(matches, fio.Matches(text), "Matches");
            Assert.AreEqual(complete, fio.IsComplete(text), "IsComplete");
        }
    }
}