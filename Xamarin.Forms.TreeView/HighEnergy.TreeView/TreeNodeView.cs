using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using Xamarin.Forms;
using HighEnergy.Collections;

namespace HighEnergy.Controls
{
    // analog to ITreeNode<T>
    public partial class TreeNodeView : StackLayout
    {
        Grid _mainLayoutGrid;
        ContentView _headerView;
        StackLayout _childrenStackLayout;

        TreeNodeView ParentTreeNodeView { get; set; }

        public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create("IsExpanded", typeof(bool), typeof(TreeNodeView), true, BindingMode.TwoWay, null, 
            (bindable, oldValue, newValue) =>
            {
                var node = bindable as TreeNodeView;

                if (oldValue == newValue || node == null)
                    return;

                node.BatchBegin();
                try
                {
                    // show or hide all children
                    node._childrenStackLayout.IsVisible = node.IsExpanded;
                }
                finally
                {
                    // ensure we commit
                    node.BatchCommit();
                }
            }
            , null, null);

        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public View HeaderContent
        {
            get { return _headerView.Content; }
            set { _headerView.Content = value; }
        }

        public IEnumerable<TreeNodeView> ChildTreeNodeViews
        {
            get
            {
                foreach (TreeNodeView view in _childrenStackLayout.Children)
                    yield return view;

                yield break;
            }
        }

        protected void DetachVisualChildren()
        {
			var views = _childrenStackLayout.Children.OfType<TreeNodeView>().ToList();

			foreach (TreeNodeView nodeView in views)
            {
                _childrenStackLayout.Children.Remove(nodeView);
                nodeView.ParentTreeNodeView = null;
            }
        }

        protected override void OnBindingContextChanged()
        {
            // prevent exceptions for null binding contexts
            // and during startup, this node will inherit its BindingContext from its Parent - ignore this
            if (BindingContext == null || (Parent != null && BindingContext == Parent.BindingContext))
                return;
			
            var node = BindingContext as ITreeNode;
            if (node == null)
                throw new InvalidOperationException("TreeNodeView currently only supports TreeNode-derived data binding sources.");

			base.OnBindingContextChanged();

			// clear out any existing child nodes - the new data source replaces them
            // make sure we don't do this if BindingContext == null
            DetachVisualChildren();

            // build the new visual tree
            BuildVisualChildren();
        }

        Func<View> _headerCreationFactory;
        public Func<View> HeaderCreationFactory
        {
            // [recursive up] inherit property value from parent if null
            get
            { 
                if (_headerCreationFactory != null)
                    return _headerCreationFactory;

                if (ParentTreeNodeView != null)
                    return ParentTreeNodeView.HeaderCreationFactory;

                return null;
            }
            set
            {
                if (value == _headerCreationFactory)
                    return;

                _headerCreationFactory = value;
                OnPropertyChanged("HeaderCreationFactory");

                // wait until both factories are assigned before constructing the visual tree
                if (_headerCreationFactory == null || _nodeCreationFactory == null)
                    return;

                BuildHeader();
                BuildVisualChildren();
            }
        }

        Func<TreeNodeView> _nodeCreationFactory;
        public Func<TreeNodeView> NodeCreationFactory
        {
            // [recursive up] inherit property value from parent if null
            get
            { 
                if (_nodeCreationFactory != null)
                    return _nodeCreationFactory;

                if (ParentTreeNodeView != null)
                    return ParentTreeNodeView.NodeCreationFactory;

                return null;
            }
            set
            {
                if (value == _nodeCreationFactory)
                    return;

                _nodeCreationFactory = value;
                OnPropertyChanged("NodeCreationFactory");

                // wait until both factories are assigned before constructing the visual tree
                if (_headerCreationFactory == null || _nodeCreationFactory == null)
                    return;

                BuildHeader();
                BuildVisualChildren();
            }
        }

        protected void BuildHeader()
        {
            // the new HeaderContent will inherit its BindingContext from this.BindingContext [recursive down]
            if (HeaderCreationFactory != null)
                HeaderContent = HeaderCreationFactory.Invoke();
        }

        // [recursive down] create item template instances, attach and layout, and set descendents until finding overrides
        protected void BuildVisualChildren()
        {
            var bindingContextNode = (ITreeNode)BindingContext;
            if (bindingContextNode == null)
                return;

            // STEP 1: remove child visual tree nodes (TreeNodeViews) that don't correspond to an item in our data source

            var nodeViewsToRemove = new List<TreeNodeView>();

            var bindingChildList = new List<ITreeNode>(bindingContextNode != null ? bindingContextNode.ChildNodes : null);

            // which ChildTreeNodeViews are in the visual tree... ?
            foreach (TreeNodeView nodeView in ChildTreeNodeViews)
                // but missing from the bound data source?
                if (!bindingChildList.Contains(nodeView.BindingContext))
                    // tag them for removal from the visual tree
                    nodeViewsToRemove.Add(nodeView);

            BatchBegin();
            try
            {
                // perform removal in a batch
                foreach (TreeNodeView nodeView in nodeViewsToRemove)
                    _mainLayoutGrid.Children.Remove(nodeView);
            }
            finally
            {
                // ensure we commit
                BatchCommit();
            }

            // STEP 2: add visual tree nodes (TreeNodeViews) for children of the binding context not already associated with a TreeNodeView

            if (NodeCreationFactory != null)
            {
                var nodeViewsToAdd = new Dictionary<TreeNodeView,ITreeNode>();

                foreach (ITreeNode node in bindingContextNode.ChildNodes)
                {
                    if (!ChildTreeNodeViews.Any(nodeView => nodeView.BindingContext == node))
                    {
                        var nodeView = NodeCreationFactory.Invoke();
                        nodeView.ParentTreeNodeView = this;

                        if (HeaderCreationFactory != null)
                            nodeView.HeaderContent = HeaderCreationFactory.Invoke();

                        // the order of these may be critical
                        nodeViewsToAdd.Add(nodeView, node);
                    }
                }

                BatchBegin();
                try
                {
                    // perform the additions in a batch
                    foreach (KeyValuePair<TreeNodeView,ITreeNode> nodeView in nodeViewsToAdd)
                    {
                        // only set BindingContext after the node has Parent != null
                        nodeView.Key.BindingContext = nodeView.Value;
						nodeView.Value.ExpandAction = () => nodeView.Key.BuildVisualChildren();
						nodeView.Key._childrenStackLayout.IsVisible = nodeView.Key.IsExpanded;
						_childrenStackLayout.Children.Add(nodeView.Key);

						_childrenStackLayout.SetBinding(StackLayout.IsVisibleProperty, new Binding("IsExpanded", BindingMode.TwoWay));

                        // TODO: make sure to unsubscribe elsewhere
                        nodeView.Value.PropertyChanged += HandleListCountChanged;
                    }
                }
                finally
                {
                    // ensure we commit
                    BatchCommit();
                }
            }
        }

        void HandleListCountChanged(object sender, PropertyChangedEventArgs e)
        {
			Device.BeginInvokeOnMainThread(() =>
			    {
					if (e.PropertyName == "Count")
                    {
					    var nodeView = ChildTreeNodeViews.Where(nv => nv.BindingContext == sender).FirstOrDefault();
                        if (nodeView != null)
                            nodeView.BuildVisualChildren();
                    }
				});
        }

		public void InitializeComponent()
        {
            IsExpanded = true;

            _mainLayoutGrid = new Grid
                {
                    VerticalOptions = LayoutOptions.StartAndExpand,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    BackgroundColor = Color.Gray,
                    RowSpacing = 2
                };
            _mainLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _mainLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _headerView = new ContentView
                {
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    BackgroundColor = this.BackgroundColor
                };
            _mainLayoutGrid.Children.Add(_headerView);

            _childrenStackLayout = new StackLayout
            {
                Orientation = this.Orientation,
                BackgroundColor = Color.Blue,
                Spacing = 0
            };
            _mainLayoutGrid.Children.Add(_childrenStackLayout, 0, 1);

            Children.Add(_mainLayoutGrid);

            Spacing = 0;
            Padding = new Thickness(0);
            HorizontalOptions = LayoutOptions.FillAndExpand;
            VerticalOptions = LayoutOptions.Start;
        }

        public TreeNodeView() : base()
        {
            InitializeComponent();

            Debug.WriteLine("new TreeNodeView");
        }
    }
}