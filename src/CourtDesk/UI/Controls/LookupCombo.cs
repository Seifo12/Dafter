using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;

namespace CourtDesk.UI.Controls
{
    public sealed class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Display { get { return Name; } }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    public sealed class LookupCombo : ComboBox
    {
        private readonly Func<List<LookupItem>> _source;
        private readonly Func<string, int> _add;

        public LookupCombo(Func<List<LookupItem>> source, Func<string, int> add)
        {
            _source = source;
            _add = add;
            FontSize = 22;
            MinHeight = 56;
            Margin = new Thickness(6);
        }

        public void Reload()
        {
            int keep = SelectedId;
            ItemsSource = _source();
            if (keep > 0)
            {
                SelectedId = keep;
            }
        }

        public int SelectedId
        {
            get
            {
                var item = SelectedItem as LookupItem;
                return item == null ? 0 : item.Id;
            }
            set
            {
                foreach (object o in Items)
                {
                    var item = o as LookupItem;
                    if (item != null && item.Id == value)
                    {
                        SelectedItem = item;
                        return;
                    }
                }
                SelectedItem = null;
            }
        }

        public bool AddInline(string text)
        {
            if (_add == null || string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                return false;
            }
            int id = _add(text);
            Reload();
            SelectedId = id;
            return true;
        }
    }
}