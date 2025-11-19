namespace UnityPerfProfilerWPF.Utils;

public class MessageMeter
{
    private readonly object _lock = new object();
    private readonly int[] _dataPoints;
    private readonly int _capacity;
    private int _currentIndex;
    private int _totalCount;
    
    public MessageMeter(int capacity = 30)
    {
        _capacity = capacity;
        _dataPoints = new int[capacity];
        _currentIndex = 0;
        _totalCount = 0;
    }
    
    public void IncreaseCount(int bytes)
    {
        lock (_lock)
        {
            _dataPoints[_currentIndex] += bytes;
            _totalCount += bytes;
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_dataPoints, 0, _dataPoints.Length);
            _currentIndex = 0;
            _totalCount = 0;
        }
    }
    
    public int[] GetMeterData()
    {
        lock (_lock)
        {
            var result = new int[_capacity];
            Array.Copy(_dataPoints, result, _capacity);
            return result;
        }
    }
    
    public void Tick()
    {
        lock (_lock)
        {
            _currentIndex = (_currentIndex + 1) % _capacity;
            _dataPoints[_currentIndex] = 0;
        }
    }
    
    public int GetCurrentTotal()
    {
        lock (_lock)
        {
            return _totalCount;
        }
    }
    
    public int GetCurrentSecond()
    {
        lock (_lock)
        {
            return _dataPoints[_currentIndex];
        }
    }
}
