using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Vision.Behaviors
{
    public class GridAutoScrollBehavior : Behavior<DataGrid>
    {
        private INotifyCollectionChanged? _collection;

        public bool IsAutoScroll
        {
            get => (bool)GetValue(IsAutoSrollProperty);
            set => SetValue(IsAutoSrollProperty, value);
        }





        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsAutoSrollProperty =
            DependencyProperty.Register(nameof(IsAutoScroll), typeof(bool), typeof(GridAutoScrollBehavior), new FrameworkPropertyMetadata(false, OnIsAutoScrollChanged));

        private static void OnIsAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GridAutoScrollBehavior)d).Subscribe();
        }

        public void Subscribe()
        {
            UnSubscribe();

            if (!IsAutoScroll || AssociatedObject == null) return;
            if (AssociatedObject.ItemsSource is INotifyCollectionChanged coll)
            {
                _collection = coll;
                _collection.CollectionChanged += OnCollectionChanged;
            }
        }

        private void UnSubscribe()
        {
            if (_collection != null)
            {
                _collection.CollectionChanged -= OnCollectionChanged;
                _collection = null;
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;

            AssociatedObject?.Dispatcher.BeginInvoke(() =>
            {
                if (AssociatedObject.Items.Count > 0)
                {
                    AssociatedObject.ScrollIntoView(AssociatedObject.Items[^1]);
                }
            });

        }
    }
}
