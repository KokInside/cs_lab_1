using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Diagnostics;

namespace csWinFkey
{
    /// <summary>
    /// Автоматизированный ввод списка ФИО «на лету» по задаваемому шаблону.
    ///
    /// Порядок работы:
    ///   1. Первая строка — шаблон. Вводится свободно и сохраняется по Enter
    ///      (кодирование шаблона описано в классе <see cref="FioMask"/>).
    ///   2. Далее каждая строка — очередная ФИО, редактируемая по шаблону:
    ///      регистр букв расставляется автоматически, разделители (пробелы,
    ///      точки, запятые и т.п.) дописываются сами. Слово заканчивается
    ///      пробелом (или Enter), инициал — сразу после ввода буквы.
    ///   3. После ввода последнего поля вся строка выделяется. Backspace —
    ///      исправление; ввод следующей буквы — начало новой ФИО (предыдущая
    ///      отправляется в журнал/консоль).
    ///   4. Esc — вернуться к редактированию шаблона.
    /// </summary>
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            TextBox1.KeyPress += new KeyPressEventHandler(TextBox1_KeyPress);
            TextBox1.KeyUp += new KeyEventHandler(TextBox1_KeyUp);
            label1.Text = "Введите шаблон (Ф/И/О — слово, ф/и/о — инициал) и нажмите Enter";
        }

        /// <summary>Сохранённый шаблон; null — режим ввода шаблона.</summary>
        FioMask? fio = null;

        /// <summary>Текущая ФИО введена полностью (строка выделена).</summary>
        bool entryDone = false;

        /// <summary>Введённые ФИО («консоль»).</summary>
        readonly List<string> entries = new List<string>();

        private void TextBox1_KeyPress(object? sender, KeyPressEventArgs e)
        {
            char c = e.KeyChar;

            // Режим ввода шаблона — свободный ввод, Enter/Esc разбираются в KeyUp.
            if (fio == null)
            {
                if (c == '\r' || c == (char)Keys.Escape)
                    e.Handled = true;
                return;
            }

            if (c == (char)Keys.Escape)
            {
                e.Handled = true;              // обрабатывается в KeyUp
                return;
            }

            if (c == '\b')                     // Backspace — стандартная обработка,
            {                                  // строка снова считается незавершённой
                entryDone = false;
                return;
            }

            if (char.IsControl(c) && c != '\r')
                return;

            e.Handled = true;

            string text = TextBox1.Text;
            int selStart = TextBox1.SelectionStart;
            int selLength = TextBox1.SelectionLength;
            bool allSelected = text.Length > 0 && selLength == text.Length;

            if (selStart + selLength != text.Length)
            {
                label1.Text = "Ввод возможен только в конце строки (исправляйте через Backspace)";
                return;
            }

            string prefix = text.Substring(0, selStart);

            // Начало новой ФИО: вся строка выделена, либо предыдущая ФИО завершена.
            bool newEntry = allSelected
                            || (selLength == 0 && (entryDone || fio.IsComplete(prefix)));

            if (newEntry)
            {
                if (!char.IsLetter(c))
                    return;                    // ФИО начинается только с буквы
                CommitEntry(text);
                prefix = "";
            }

            string? add = fio.Apply(prefix, c, out bool completed);
            if (add == null)
            {
                if (!fio.Matches(prefix))
                    label1.Text = "Строка не соответствует шаблону — исправьте её (Backspace)";
                return;
            }

            TextBox1.Text = prefix + add;

            if (completed)
            {
                TextBox1.SelectionStart = 0;
                TextBox1.SelectionLength = TextBox1.Text.Length;
                entryDone = true;
            }
            else
            {
                TextBox1.SelectionStart = TextBox1.Text.Length;
                TextBox1.SelectionLength = 0;
            }
        }

        private void TextBox1_KeyUp(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && fio == null)
            {
                if (FioMask.CheckTemplate(TextBox1.Text))
                {
                    fio = new FioMask(TextBox1.Text);
                    entryDone = false;
                    label1.Text = "шаблон сохранён: " + fio.Source;
                    TextBox1.Clear();
                }
                else
                {
                    label1.Text = "шаблон введён неверно";
                }
                return;
            }

            if (e.KeyCode == Keys.Escape && fio != null)
            {
                TextBox1.Text = fio.Source;
                label1.Text = "изменение шаблона: " + fio.Source;
                fio = null;
                entryDone = false;
                TextBox1.SelectionStart = 0;
                TextBox1.SelectionLength = TextBox1.Text.Length;
            }
        }

        /// <summary>
        /// Отправляет завершённую ФИО на консоль» (окно Output отладчика,
        /// консоль процесса) и показывает её в метке.
        /// </summary>
        private void CommitEntry(string text)
        {
            if (fio != null && (entryDone || fio.IsComplete(text)))
            {
                entries.Add(text);
                Debug.WriteLine(text);
                Console.WriteLine(text);
                label1.Text = $"шаблон: {fio.Source}   введено: {entries.Count}, последняя: {text}";
            }
            entryDone = false;
        }
    }

    /// <summary>
    /// Шаблон вводимой строки ФИО.
    ///
    /// Кодирование шаблона:
    ///   Ф, И, О — поле «целое слово» (фамилия, имя, отчество): первая буква
    ///             прописная, остальные строчные; допускается один дефис
    ///             (двойная фамилия «Новиков-Прибой»);
    ///   ф, и, о — поле «инициал»: одна прописная буква;
    ///   любые другие НЕбуквенные символы — разделители, которые
    ///             дописываются в строку автоматически.
    ///
    /// Требования к шаблону: каждое из полей Ф, И, О встречается ровно один раз;
    /// других букв нет; после поля-слова обязательно стоит разделитель
    /// (иначе нельзя понять, где слово кончилось).
    ///
    /// Примеры:
    ///   "Ф и.о."   → Новиков-Прибой А.С.   (базовый вариант из задания)
    ///   "и.о. Ф"   → А.С. Новиков-Прибой
    ///   "Ф И О"    → Новиков Алексей Силыч
    ///   "Ф, И о."  → Новиков, Алексей С.
    /// </summary>
    public sealed class FioMask
    {
        /// <summary>Элемент шаблона: поле или разделитель.</summary>
        private sealed class Token
        {
            public bool IsField;          // true — поле, false — разделитель
            public bool IsWord;           // поле: true — слово, false — инициал
            public string Literal = "";   // разделитель: вставляемый текст
        }

        /// <summary>Положение в шаблоне после разбора уже введённого текста.</summary>
        private struct State
        {
            public int Token;             // индекс текущего элемента шаблона
            public int LitPos;            // сколько символов разделителя уже введено
            public int WordLen;           // сколько букв текущего слова введено
            public bool HyphenUsed;       // в слове уже есть дефис
            public bool LastHyphen;       // последний символ слова — дефис
        }

        private readonly List<Token> tokens = new List<Token>();

        /// <summary>Исходный текст шаблона.</summary>
        public string Source { get; }

        public FioMask(string mask)
        {
            Source = mask;
            var lit = new StringBuilder();
            foreach (char c in mask)
            {
                if (IsFieldChar(c))
                {
                    if (lit.Length > 0)
                    {
                        tokens.Add(new Token { Literal = lit.ToString() });
                        lit.Clear();
                    }
                    tokens.Add(new Token { IsField = true, IsWord = char.IsUpper(c) });
                }
                else
                {
                    lit.Append(c);
                }
            }
            if (lit.Length > 0)
                tokens.Add(new Token { Literal = lit.ToString() });
        }

        private static bool IsFieldChar(char c) =>
            c == 'Ф' || c == 'ф' || c == 'И' || c == 'и' || c == 'О' || c == 'о';

        /// <summary>Проверка корректности шаблона (см. описание класса).</summary>
        public static bool CheckTemplate(string mask)
        {
            if (string.IsNullOrEmpty(mask))
                return false;

            int countF = 0, countI = 0, countO = 0;
            int lastFieldIndex = -1;       // позиция предыдущей буквы-поля
            bool lastFieldIsWord = false;

            for (int i = 0; i < mask.Length; i++)
            {
                char c = mask[i];

                if (!IsFieldChar(c))
                {
                    if (char.IsLetter(c))
                        return false;      // посторонняя буква
                    continue;
                }

                if (c == 'Ф' || c == 'ф') countF++;
                else if (c == 'И' || c == 'и') countI++;
                else countO++;

                // после слова вплотную не может стоять другое поле:
                // конец слова обозначается разделителем
                if (lastFieldIndex == i - 1 && lastFieldIsWord)
                    return false;

                lastFieldIndex = i;
                lastFieldIsWord = char.IsUpper(c);
            }

            return countF == 1 && countI == 1 && countO == 1;
        }

        /// <summary>Соответствует ли текст началу шаблона.</summary>
        public bool Matches(string text) => TryParse(text, out _);

        /// <summary>Текст полностью соответствует шаблону (все поля введены).</summary>
        public bool IsComplete(string text) =>
            TryParse(text, out State st) && st.Token == tokens.Count;

        /// <summary>
        /// Обрабатывает символ <paramref name="c"/>, введённый после текста
        /// <paramref name="prefix"/>. Возвращает строку, которую нужно дописать
        /// (с учётом регистра и автоматических разделителей), или null, если
        /// символ по шаблону недопустим. <paramref name="completed"/> — после
        /// дописывания введены все поля шаблона.
        /// </summary>
        public string? Apply(string prefix, char c, out bool completed)
        {
            completed = false;
            if (!TryParse(prefix, out State st))
                return null;

            var sb = new StringBuilder();
            if (!Step(ref st, c, sb))
                return null;

            completed = st.Token == tokens.Count;
            return sb.ToString();
        }

        /// <summary>Переход к следующему элементу шаблона.</summary>
        private static void Next(ref State st)
        {
            st.Token++;
            st.LitPos = 0;
            st.WordLen = 0;
            st.HyphenUsed = false;
            st.LastHyphen = false;
        }

        /// <summary>Если текущий элемент — разделитель, дописать его целиком.</summary>
        private void AppendLiteral(ref State st, StringBuilder sb)
        {
            if (st.Token < tokens.Count && !tokens[st.Token].IsField)
            {
                sb.Append(tokens[st.Token].Literal);
                Next(ref st);
            }
        }

        /// <summary>Разделитель, следующий за элементом index ("" — нет).</summary>
        private string LiteralAfter(int index) =>
            index + 1 < tokens.Count && !tokens[index + 1].IsField
                ? tokens[index + 1].Literal
                : "";

        /// <summary>
        /// Дефис внутри слова разрешён, если сам разделитель после слова
        /// не начинается с дефиса (иначе дефис заканчивает слово).
        /// </summary>
        private bool HyphenAllowed(int index) => !LiteralAfter(index).StartsWith("-");

        /// <summary>Один шаг автомата: обработка введённого символа.</summary>
        private bool Step(ref State st, char c, StringBuilder sb)
        {
            if (st.Token == tokens.Count)
                return false;              // все поля уже введены

            Token tk = tokens[st.Token];

            // Каретка внутри (или перед) разделителем — дописываем его остаток.
            if (!tk.IsField)
            {
                bool sepKey = c == ' ' || c == '\r' || c == tk.Literal[st.LitPos];
                if (!char.IsLetter(c) && !sepKey)
                    return false;

                sb.Append(tk.Literal, st.LitPos, tk.Literal.Length - st.LitPos);
                Next(ref st);

                if (!char.IsLetter(c) || st.Token == tokens.Count)
                    return true;           // клавиша-разделитель поглощена
                tk = tokens[st.Token];     // буква пойдёт в следующее поле
            }

            if (!tk.IsWord)
            {
                // Инициал: одна прописная буква, затем сразу разделитель.
                if (!char.IsLetter(c))
                    return false;
                sb.Append(char.ToUpper(c));
                Next(ref st);
                AppendLiteral(ref st, sb);
                return true;
            }

            // Слово.
            if (char.IsLetter(c))
            {
                bool upper = st.WordLen == 0 || st.LastHyphen;
                sb.Append(upper ? char.ToUpper(c) : char.ToLower(c));
                st.WordLen++;
                st.LastHyphen = false;
                return true;
            }

            if (c == '-' && HyphenAllowed(st.Token))
            {
                if (st.WordLen == 0 || st.LastHyphen || st.HyphenUsed)
                    return false;
                sb.Append('-');
                st.HyphenUsed = true;
                st.LastHyphen = true;
                return true;
            }

            string after = LiteralAfter(st.Token);
            bool endKey = c == ' ' || c == '\r' || (after.Length > 0 && c == after[0]);
            if (endKey)
            {
                // Конец слова: оно не может быть пустым или оканчиваться дефисом.
                if (st.WordLen == 0 || st.LastHyphen)
                    return false;
                Next(ref st);
                AppendLiteral(ref st, sb);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Разбор уже введённого текста по шаблону: определяет текущее положение
        /// (благодаря этому корректно работают исправления через Backspace).
        /// Возвращает false, если текст не соответствует шаблону.
        /// </summary>
        private bool TryParse(string text, out State st)
        {
            st = new State();
            int i = 0;
            while (i < text.Length)
            {
                if (st.Token == tokens.Count)
                    return false;          // лишние символы после конца шаблона

                Token tk = tokens[st.Token];
                char ch = text[i];

                if (!tk.IsField)
                {
                    if (ch != tk.Literal[st.LitPos])
                        return false;
                    st.LitPos++;
                    i++;
                    if (st.LitPos == tk.Literal.Length)
                        Next(ref st);
                }
                else if (!tk.IsWord)
                {
                    if (!char.IsLetter(ch))
                        return false;
                    i++;
                    Next(ref st);
                }
                else if (char.IsLetter(ch))
                {
                    st.WordLen++;
                    st.LastHyphen = false;
                    i++;
                }
                else if (ch == '-' && HyphenAllowed(st.Token)
                         && st.WordLen > 0 && !st.LastHyphen && !st.HyphenUsed)
                {
                    st.HyphenUsed = true;
                    st.LastHyphen = true;
                    i++;
                }
                else
                {
                    // Слово закончилось — символ должен относиться к разделителю.
                    if (st.WordLen == 0 || st.LastHyphen)
                        return false;
                    Next(ref st);          // i не сдвигаем
                }
            }
            return true;
        }
    }
}