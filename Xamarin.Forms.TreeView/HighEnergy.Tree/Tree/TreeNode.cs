using System;
using System.Linq;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Input;

namespace HighEnergy.Collections {
  public class TreeNode<T> :ObservableObject, ITreeNode<T>, IDisposable
      where T : new() {
    public TreeNode ( ) {
      // call property setters to trigger setup and event notifications
      _parent = null;
      _childNodes = new TreeNodeList<T> (this);
      _childNodes.PropertyChanged += (object sender, PropertyChangedEventArgs e) => {
        OnPropertyChanged (e.PropertyName);
      };
    }


    public TreeNode (T Value)
        : this ( ) {
      // call property setters to trigger setup and event notifications
      this.Value = Value;
    }

    public TreeNode (T Value, TreeNode<T> Parent)
    : this (Value) {
      // call property setters to trigger setup and event notifications
      _parent = Parent;
    }

    public ITreeNode ParentNode {
      get { return _parent; }
    }

    private ITreeNode<T> _parent;
    public ITreeNode<T> Parent {
      get { return _parent; }
      set { SetParent (value, true); }
    }

    public void SetParent (ITreeNode<T> node, bool updateChildNodes = true) {
      if (node == Parent)
        return;

      var oldParent = Parent;
      var oldParentHeight = Parent != null ? Parent.Height : 0;
      var oldDepth = Depth;

      // if oldParent isn't null
      // remove this node from its newly ex-parent's children
      if (oldParent != null && oldParent.Children.Contains (this))
        oldParent.Children.Remove (this, updateParent: false);

      // update the backing field
      _parent = node;

      // add this node to its new parent's children
      if (_parent != null && updateChildNodes)
        _parent.Children.Add (this, updateParent: false);

      // signal the old parent that it has lost this child
      if (oldParent != null)
        oldParent.OnDescendantChanged (NodeChangeType.NodeRemoved, this);

      if (oldDepth != Depth)
        OnDepthChanged ( );

      // if this operation has changed the height of any parent, initiate the bubble-up height changed event
      if (Parent != null) {
        var newParentHeight = Parent != null ? Parent.Height : 0;
        if (newParentHeight != oldParentHeight)
          Parent.OnHeightChanged ( );

        Parent.OnDescendantChanged (NodeChangeType.NodeAdded, this);
      }

      OnParentChanged (oldParent, Parent);
    }

    protected virtual void OnParentChanged (ITreeNode<T> oldValue, ITreeNode<T> newValue) {
      OnPropertyChanged ("Parent");
    }

    // TODO: add property and event notifications that are missing from this set: DescendentsChanged, AnscestorsChanged, ChildrenChanged, ParentChanged

    public ITreeNode<T> Root {
      get { return (Parent == null) ? this : Parent.Root; }
    }

    private TreeNodeList<T> _childNodes;
    public TreeNodeList<T> Children {
      get { return _childNodes; }
    }

    // non-generic iterator for interface-based support (From TreeNodeView, for example)
    public IEnumerable<ITreeNode> ChildNodes {
      get {
        foreach (ITreeNode node in Children)
          yield return node;

        yield break;
      }
    }

    public IEnumerable<ITreeNode> Descendants {
      get {
        foreach (ITreeNode node in ChildNodes) {
          yield return node;

          foreach (ITreeNode descendant in node.Descendants)
            yield return descendant;
        }

        yield break;
      }
    }

    public IEnumerable<ITreeNode> Subtree {
      get {
        yield return this;

        foreach (ITreeNode node in Descendants)
          yield return node;

        yield break;
      }
    }

    public IEnumerable<ITreeNode> Ancestors {
      get {
        if (Parent == null)
          yield break;

        yield return Parent;

        foreach (ITreeNode node in Parent.Ancestors)
          yield return node;

        yield break;
      }
    }

    public event Action<NodeChangeType, ITreeNode> AncestorChanged;
    public virtual void OnAncestorChanged (NodeChangeType changeType, ITreeNode node) {
      if (AncestorChanged != null)
        AncestorChanged (changeType, node);

      foreach (ITreeNode<T> child in Children)
        child.OnAncestorChanged (changeType, node);
    }

    public event Action<NodeChangeType, ITreeNode> DescendantChanged;
    public virtual void OnDescendantChanged (NodeChangeType changeType, ITreeNode node) {
      if (DescendantChanged != null)
        DescendantChanged (changeType, node);

      if (Parent != null)
        Parent.OnDescendantChanged (changeType, node);
    }

    // [recurse up] descending aggregate property
    public int Height {
      get { return Children.Count == 0 ? 0 : Children.Max (n => n.Height) + 1; }
    }

    // [recurse down] descending-broadcasting event
    public virtual void OnHeightChanged ( ) {
      OnPropertyChanged ("Height");

      foreach (ITreeNode<T> child in Children)
        child.OnHeightChanged ( );
    }

    private T _value;
    public T Value {
      get { return _value; }
      set {
        if (value == null && _value == null)
          return;

        if (value != null && _value != null && value.Equals (_value))
          return;

        _value = value;
        OnPropertyChanged ("Value");

        // set Node if it's ITreeNodeAware
        if (_value != null && _value is ITreeNodeAware<T>)
          (_value as ITreeNodeAware<T>).Node = this;
      }
    }

    // [recurse up] bubble up aggregate property
    public int Depth {
      get { return (Parent == null ? 0 : Parent.Depth + 1); }
    }

    // [recurse up] bubble up event
    public virtual void OnDepthChanged ( ) {
      OnPropertyChanged ("Depth");

      if (Parent != null)
        Parent.OnDepthChanged ( );
    }

    private UpDownTraversalType _disposeTraversal = UpDownTraversalType.BottomUp;
    public UpDownTraversalType DisposeTraversal {
      get { return _disposeTraversal; }
      set { _disposeTraversal = value; }
    }

    private bool _isDisposed;
    public bool IsDisposed {
      get { return _isDisposed; }
    }

    public Action ExpandAction { get; set; }

    public virtual void Dispose ( ) {
      CheckDisposed ( );
      OnDisposing ( );

      // clean up contained objects (in Value property)
      if (Value is IDisposable) {
        if (DisposeTraversal == UpDownTraversalType.BottomUp)
          foreach (TreeNode<T> node in Children)
            node.Dispose ( );

        (Value as IDisposable).Dispose ( );

        if (DisposeTraversal == UpDownTraversalType.TopDown)
          foreach (TreeNode<T> node in Children)
            node.Dispose ( );
      }

      _isDisposed = true;
    }

    public event EventHandler Disposing;

    protected void OnDisposing ( ) {
      if (Disposing != null)
        Disposing (this, EventArgs.Empty);
    }

    public void CheckDisposed ( ) {
      if (IsDisposed)
        throw new ObjectDisposedException (GetType ( ).Name);
    }

    public override string ToString ( ) {
      return "Depth=" + Depth + ", Height=" + Height + ", Children=" + Children.Count;
    }
  }
}