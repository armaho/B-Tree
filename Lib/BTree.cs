using System.Collections;
using System.Net.Mime;

namespace Lib;

public class BTree<TValue> : ICollection<TValue>, IReadOnlyCollection<TValue>
{
    #region Nested Classes

    private class BTreeNode : IEnumerable<TValue>
    {
        #region Fields

        private bool _isLeaf;
        private BTree<TValue> _tree;
        private List<TValue> _items;
        private List<BTreeNode> _children;

        #endregion

        #region Constructor

        public BTreeNode(BTree<TValue> tree, bool isLeaf, List<TValue>? items = null, List<BTreeNode>? children = null)
        {
            _isLeaf = isLeaf;
            _tree = tree;
            _items = items ?? new List<TValue>();
            _children = children ?? new List<BTreeNode>();
        }

        #endregion

        #region Properties

        public bool IsFull => _items.Count == (_tree._minDegree << 1) - 1;

        public bool IsEmpty => _items.Count == 0;

        public IReadOnlyList<BTreeNode> Children => _children;

        public IReadOnlyList<TValue> Items => _items;

        private bool OverMinDegree => _items.Count >= _tree._minDegree + 1;

        #endregion

        #region Methods
        
        public IEnumerator<TValue> GetEnumerator()
        {
            if (_isLeaf)
            {
                foreach (var item in _items)
                {
                    yield return item;
                }
            }
            else
            {
                for (int i = 0; i < _children.Count; i++)
                {
                    foreach (var item in _children[i])
                    {
                        yield return item;
                    }

                    if (i == _items.Count)
                    {
                        break;
                    }

                    yield return _items[i];
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public bool Contains(TValue item)
        {
            var searchResult = _items.BinarySearch(item, _tree._comparer);
            
            return (searchResult >= 0) || (!_isLeaf && _children[~searchResult].Contains(item));
        }

        public void Add(TValue item)
        {
            if (_isLeaf)
            {
                var searchResult = _items.BinarySearch(item, _tree._comparer);

                _items.Insert((searchResult >= 0) ? searchResult : ~searchResult, item);
            }
            else
            {
                var searchResult = _items.BinarySearch(item, _tree._comparer);
                var childIdx = (searchResult >= 0) ? searchResult : ~searchResult;

                if (_children[childIdx].IsFull)
                {
                    SplitChild(childIdx);

                    if (_tree._comparer.Compare(_items[childIdx], item) < 0)
                    {
                        childIdx++;
                    }
                }
                
                _children[childIdx].Add(item);
            }
        }

        public bool Remove(TValue item)
        {
            var searchResult = _items.BinarySearch(item, _tree._comparer);
            
            if (_isLeaf)
            {
                if (searchResult < 0)
                {
                    return false;
                }
                
                _items.RemoveAt(searchResult);
                return true;
            }

            if (searchResult >= 0)
            {
                if (_children[searchResult].OverMinDegree)
                {
                    var replacement = _children[searchResult].Max();

                    _items[searchResult] = replacement;
                    return _children[searchResult].Remove(replacement);
                }

                if (_children[searchResult + 1].OverMinDegree)
                {
                    var replacement = _children[searchResult + 1].Min();

                    _items[searchResult] = replacement;
                    return _children[searchResult + 1].Remove(replacement);
                }

                _children[searchResult]._items.Add(item);
                _children[searchResult]._items.AddRange(_children[searchResult + 1]._items);
                _children[searchResult]._children.AddRange(_children[searchResult + 1]._children);
                
                _items.RemoveAt(searchResult);
                _children.RemoveAt(searchResult + 1);
                
                return _children[searchResult].Remove(item);
            }

            searchResult = ~searchResult;

            if (_children[searchResult].OverMinDegree)
            {
                return _children[searchResult].Remove(item);
            }
            
            if ((searchResult + 1 < _children.Count) && _children[searchResult + 1].OverMinDegree)
            {
                var sibling = _children[searchResult + 1];
                    
                _children[searchResult]._items.Add(_items[searchResult]);
                _children[searchResult]._children.Add(sibling._children[0]);

                _items[searchResult] = sibling._items[0];
                    
                sibling._items.RemoveAt(0);
                sibling._children.RemoveAt(0);
            } 
            else if ((0 < searchResult - 1) && _children[searchResult - 1].OverMinDegree)
            {
                var sibling = _children[searchResult - 1];
                    
                _children[searchResult]._items.Insert(0, _items[searchResult - 1]);
                _children[searchResult]._children.Insert(0, sibling._children[^1]);

                _items[searchResult - 1] = sibling._items[^1];
                    
                sibling._items.RemoveAt(sibling._items.Count - 1);
                sibling._children.RemoveAt(sibling._children.Count - 1);
            }
            else
            {
                if (_children.Count <= searchResult + 1)
                {
                    searchResult--;
                }

                var sibling = _children[searchResult + 1];
                    
                _children[searchResult]._items.Add(_items[searchResult]);
                _children[searchResult]._items.AddRange(sibling._items);
                _children[searchResult]._children.AddRange(sibling._children);
                    
                _items.RemoveAt(searchResult);
                _children.RemoveAt(searchResult + 1);
            }

            return _children[searchResult].Remove(item);
        }

        public TValue Min()
        {
            return _isLeaf ? _items[0] : _children[0].Min();
        }

        public TValue Max()
        {
            return _isLeaf ? _items[^1] : _children[^1].Max();
        }
        
        //This method is called when this node isn't full but one of the children is.
        private void SplitChild(int childIdx)
        {
            if (IsFull)
            {
                throw new InvalidOperationException("The node is full.");
            }

            var splitResult = _children[childIdx].Split();
            
            _items.Insert(childIdx, splitResult.Median);
            _children.Insert(childIdx + 1, splitResult.NewNode);
        }

        //This method splits the node in half if it's full, returning its median value and the new node that contains the latter half of values
        private (TValue Median, BTreeNode NewNode) Split()
        {
            var minDegree = _tree._minDegree;
            
            if (!IsFull)
            {
                throw new InvalidOperationException("The node is not full.");
            }

            var midIdx = minDegree - 1;

            var median = _items[midIdx];
            var newNode = new BTreeNode(_tree, 
                _isLeaf, 
                items: _items.GetRange(minDegree, minDegree - 1),
                children: _isLeaf ? new List<BTreeNode>() : _children.GetRange(minDegree, minDegree));
            
            _items.RemoveRange(midIdx, minDegree);
            if (!_isLeaf)
            {
                _children.RemoveRange(midIdx + 1, minDegree);
            }

            return (Median: median, NewNode: newNode);
        }

        #endregion
    }

    #endregion

    #region Fields

    private readonly int _minDegree;
    private readonly IComparer<TValue> _comparer;
    private BTreeNode _root;
    
    #endregion

    #region Properties

    public int Count { get; private set; } = 0;
    public bool IsReadOnly { get; } = false;

    #endregion

    #region Constructors

    public BTree(int minDegree) : this(minDegree, Comparer<TValue>.Default) {}

    public BTree(int minDegree, IComparer<TValue> comparer)
    {
        _minDegree = minDegree;
        _comparer = comparer;
        _root = new BTreeNode(tree: this, isLeaf: true);
    }

    #endregion

    #region Methods

    public IEnumerator<TValue> GetEnumerator()
    {
        return _root.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(TValue item)
    {
        if (_root.IsFull)
        {
            _root = new BTreeNode(tree: this,
                isLeaf: false,
                items: new List<TValue>(),
                children: new List<BTreeNode>(new BTreeNode[] { _root }));
        }

        Count++;
        _root.Add(item);
    }

    public void Clear()
    {
        _root = new BTreeNode(this, true);
    }

    public bool Contains(TValue item)
    {
        return _root.Contains(item);
    }

    public void CopyTo(TValue[] array, int arrayIndex)
    {
        foreach (var item in this)
        {
            array[arrayIndex] = item;
            arrayIndex++;
        }
    }

    public bool Remove(TValue item)
    {
        var result = _root.Remove(item);

        if (_root.IsEmpty)
        {
            _root = _root.Children[0];
        }

        return result;
    }
    
    #endregion
}