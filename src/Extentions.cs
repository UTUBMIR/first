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

        public static int WordDifferenceMin(string word, string line) {
            if (word.Length == 0 || line.Length == 0)
                return 127;

            int minDist = int.MaxValue;
            int wlen = word.Length;

            for (int i = 0; i <= line.Length - wlen; ++i) {
                string part = line.Substring(i, wlen);
                int dist = WordDifference(word, part);
                if (dist < minDist) minDist = dist;
            }

            return minDist == int.MaxValue ? 127 : minDist;
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
                sortedList = sortedList.OrderBy(word => Extensions.WordDifference(word, typed)).ToArray();
                Console.Write($"\n>>{sortedList[0]}\n");
                if (list.Length > 1)
                    Console.Write(String.Join("\n", sortedList, 1, Math.Min(20, sortedList.Length - 1)));
            }
            Extensions.AsyncType(ShowPassedWords, text);

            Console.Clear();
            return sortedList[0];
        }

    }
}
