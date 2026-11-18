using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;

namespace First {
    static class Extensions {
        static public int GetNumber(string question, string rule, int[] diapason) {
            Console.Write(question);
            while (true) {
                if (!int.TryParse(Console.ReadLine(), out int input)) {
                    Console.Clear();
                    WriteError("\aError: number parsing failed!");
                }
                else {
                    if (input >= diapason[0] && input <= diapason[1]) {
                        return input;
                    }
                    else {
                        Console.Clear();
                        WriteError(rule, input);
                    }
                }
                Console.Write(question);
            }
        }

        static public void GetNumber(this ref int input, string question, string rule, int[] diapason) {
            Console.Write(question);
            while (true) {
                if (!int.TryParse(Console.ReadLine(), out input)) {
                    Console.Clear();
                    WriteError("\aError: number parsing failed!");
                }
                else {
                    if (input >= diapason[0] && input <= diapason[1]) {
                        return;
                    }
                    else {
                        Console.Clear();
                        WriteError(rule, input);
                    }
                }
                Console.Write(question);
            }
        }

        public static void WriteSuccess(string message) {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        static public int WordDifference(string s1, string s2) {
            int len1 = s1.Length, len2 = s2.Length;
            int[,] dp = new int[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++) dp[i, 0] = i;
            for (int j = 0; j <= len2; j++) dp[0, j] = j;

            for (int i = 1; i <= len1; i++) {
                for (int j = 1; j <= len2; j++) {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    dp[i, j] = (dp[i - 1, j] + 1 < dp[i, j - 1] + 1) ?
                            ((dp[i - 1, j] + 1 < dp[i - 1, j - 1] + cost) ? dp[i - 1, j] + 1 : dp[i - 1, j - 1] + cost)
                            :
                            ((dp[i, j - 1] + 1 < dp[i - 1, j - 1] + cost) ? dp[i, j - 1] + 1 : dp[i - 1, j - 1] + cost);
                }
            }
            return dp[len1, len2];
        }

        public static int WordDifferenceMin(string typed, string target) {
            if (string.IsNullOrEmpty(typed)) return 0;
            if (string.IsNullOrEmpty(target)) return 127;
            typed = typed.ToLower();
            target = target.ToLower();

            if (typed.Length >= target.Length) {
                return WordDifference(typed, target);
            }

            int minDist = int.MaxValue;
            int tLen = typed.Length;

            for (int i = 0; i <= target.Length - tLen; ++i) {
                string part = target.Substring(i, tLen);
                int dist = WordDifference(typed, part);
                if (dist < minDist) minDist = dist;
            }

            return minDist;
        }

        static public void WriteError(object args) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(args);
            Console.ResetColor();
        }

        static public void WriteError(string format, params object[] args) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(format, args);
            Console.ResetColor();
        }

        static public void Pause() {
            Console.Write("Press any key to continue...");
            Console.ReadKey();
        }
        static public string AsyncType(Action<string> onTextChanged, string message = "") {
            string result = "";
            int cursorPos = 0;
            Console.Write(message);
            int x = Console.CursorLeft, y = Console.CursorTop;
            onTextChanged?.Invoke(result);
            Console.SetCursorPosition(x, y);

            void Redraw() {
                int top = Console.CursorTop;
                Console.SetCursorPosition(message.Length, top);
                Console.Clear();
                Console.Write(message);
                Console.Write(result); // extra space clears leftovers
                Console.SetCursorPosition(cursorPos + message.Length, top);
            }

            while (true) {
                ConsoleKeyInfo key = Console.ReadKey(true);

                switch (key.Key) {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        return result;

                    case ConsoleKey.Backspace:
                        if (cursorPos > 0) {
                            result = result.Remove(cursorPos - 1, 1);
                            cursorPos--;
                            Redraw();
                        }
                        break;

                    case ConsoleKey.LeftArrow:
                        if (cursorPos > 0) {
                            cursorPos--;
                            Console.SetCursorPosition(cursorPos + message.Length, Console.CursorTop);
                        }
                        break;

                    case ConsoleKey.RightArrow:
                        if (cursorPos < result.Length) {
                            cursorPos++;
                            Console.SetCursorPosition(cursorPos + message.Length, Console.CursorTop);
                        }
                        break;

                    default:
                        if (!char.IsControl(key.KeyChar)) {
                            result = result.Insert(cursorPos, key.KeyChar.ToString());
                            cursorPos++;
                            Redraw();
                        }
                        break;
                }

                x = Console.CursorLeft;
                y = Console.CursorTop;
                onTextChanged?.Invoke(result);
                Console.SetCursorPosition(x, y);
            }
        }


        static public string AskForWord(string[] list, string text = "Type word to search: ") {
            string[] sortedList = (string[])list.Clone();

            void ShowPassedWords(string typed) {
                sortedList = list
                    .OrderBy(word => Extensions.WordDifferenceMin(typed, word))
                    .ThenBy(word => word.StartsWith(typed, StringComparison.OrdinalIgnoreCase) ? 0 : 1) // Prioritize prefix matches
                    .ThenBy(word => word.Length)
                    .ToArray();

                Console.Write($"\n\n > {sortedList[0]}\n");
                if (sortedList.Length > 1)
                    Console.Write(string.Join("\n", sortedList, 1, Math.Min(20, sortedList.Length - 1)));
            }

            Extensions.AsyncType(ShowPassedWords, text);

            Console.Clear();
            return sortedList[0];
        }

        static public T? AskForItem<T>(
                List<T> list,
                string prompt = "Type item to search: ",
                string emptySearchText = "Type to search items...",
                Action<T>? printItem = null,
                Func<T, string>[]? selectors = null) {

            printItem ??= item => Console.WriteLine(item);
            selectors ??= [item => item?.ToString() ?? ""];

            T? found = default;

            string query = Extensions.AsyncType(
                typed => {
                    if (string.IsNullOrWhiteSpace(typed)) {
                        Console.WriteLine();
                        Console.WriteLine($"{emptySearchText}\n");

                        foreach (var item in list.Take(6)) {
                            printItem(item);
                        }

                        if (list.Count > 6) Console.Write("...");

                        return;
                    }

                    var matches = list
                        .Select(item => (
                            item: item,
                            distance: selectors.Min(selector => Extensions.WordDifferenceMin(typed, selector(item)))
                        ))
                        .Where(x => x.distance < 5)
                        .OrderBy(x => x.distance)
                        .Select(x => x.item)
                        .ToList();

                    found = matches.Count > 0 ? matches[0] : default;

                    Console.WriteLine();

                    if (matches.Count == 0) {
                        Extensions.WriteError($"No items found matching \"{typed}\"");
                        return;
                    }

                    Extensions.WriteSuccess($"{matches.Count} match(es):\n");
                    Console.Write("> ");

                    foreach (var item in matches) {
                        printItem(item);
                    }
                },
                prompt
            );

            Console.Clear();
            return found;
        }


    }
}
