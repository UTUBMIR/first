namespace First {
    class Menu {
        public string title;
        public string[] menuItems;
        public int Length {
            get { return menuItems.Length; }
            private set { }
        }

        public Func<bool>[]? actions;

        public Menu(string title, string[] items) {
            this.title = title;
            menuItems = items;
        }

        public Menu(string title, string[] items, Func<bool>[] actions) {
            this.title = title;
            menuItems = items;
            this.actions = actions;
            if (items.Length != actions.Length) {
                throw new ArgumentException("Menu items and actions must have the same length.");
            }
        }

        public int Show() {
            Console.Clear();
            Console.WriteLine(title);
            string menu = "";
            for (int i = 0; i < Length; ++i) {
                menu += $"{i + 1,-3} {menuItems[i]}\n";
            }
            menu += "\nSelect menu item: ";
            int result = Extensions.GetNumber(menu, "\aError: {0} is out of range!", [1, Length]);
            Console.Clear();
            return result - 1;
        }

        public void Invoke() {
            if (actions == null) {
                throw new NullReferenceException("Actions must be initialized to call this method.");
            }
            int result;
            do {
                Console.Clear();
                Console.WriteLine(title);
                string menu = "";
                for (int i = 0; i < Length; ++i) {
                    menu += $"{i + 1,-3} {menuItems[i]}\n";
                }
                menu += "\nSelect menu item: ";
                result = Extensions.GetNumber(menu, "\aError: {0} is out of range!", [1, Length]);
                Console.Clear();
            } while (actions[result - 1]?.Invoke() ?? false);
        }
    }

    class UI {
        Menu[] menus;
        public int Length {
            get { return menus.Length; }
            private set { }
        }

        public UI(Menu[] menus) {
            this.menus = menus;
        }

        public void Invoke(int i) {
            if (i < 0 || i >= Length) {
                throw new ArgumentOutOfRangeException("Menu index is out of range.");
            }
            menus[i]?.Invoke();
        }
    }
}
