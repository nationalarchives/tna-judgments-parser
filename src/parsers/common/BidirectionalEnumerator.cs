
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BidirectionalEnumerator<T> : IEnumerator<T>
{
    private readonly IList<T> _list;
    private int _currentIndex;
    
    public BidirectionalEnumerator(IEnumerable<T> enmerable)
    {
        _list = enmerable.ToList();
        _currentIndex = -1;
    }
    
    public T Current => _list[_currentIndex];

    object IEnumerator.Current => Current;
    
    public bool MoveNext()
    {
        if (_currentIndex < _list.Count - 1)
        {
            _currentIndex++;
            return true;
        }
        
        return false;
    }
    
    public void Reset()
    {
        _currentIndex = -1;
    }
    
    public bool MovePrevious()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            return true;
        }
        
        return false;
    }
    
    public void Dispose()
    {
        // Not implemented
    }
}
