using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public class DoubleBufferManager<T> where T : new()
{
    private static readonly Lazy<DoubleBufferManager<T>> Ins =
        new Lazy<DoubleBufferManager<T>>(() => new DoubleBufferManager<T>());

    public static DoubleBufferManager<T> Instance => Ins.Value;

    private T _readBuffer = new T();
    private T _writeBuffer = new T();

    private bool _updating = false;

    public T GetData() => _readBuffer;

    public void SetData(T data) => _writeBuffer = data;

    public ref T WriterBuffer => ref _writeBuffer;

    public delegate void DataInit(ref T readBuffer, ref T writeBuffer);

    public delegate void WriteInit(ref T writeBuffer);
    public delegate void WriteUpdate(ref T writeBuffer, int index);
    public delegate void WriteMerge(ref T writeBuffer0, ref T writeBuffer1);

    public void CreateData(Action<T, T> action)
    {
        action(_readBuffer, _writeBuffer);
        _updating = false;
    }
    
    public void UpdateDataSlow(WriteInit action0, WriteUpdate action1, WriteMerge action2, int count)
    {
        if (count == 0)
        {
            return;
        }
        
        action0(ref _writeBuffer);
        for (int i = 0; i < count; i++)
        {
            T buffer = new T();
            action1(ref buffer, i);
            action2(ref _writeBuffer, ref buffer);
        }
        
        (_readBuffer, _writeBuffer) = (_writeBuffer, _readBuffer);
    }

    public void UpdateData(WriteInit action0, WriteUpdate action1, WriteMerge action2, int count)
    {
        if (_updating || count == 0) 
            return;
        
        lock (_writeBuffer)
        {
            _updating = true;
        }
        
        Task.Run(() =>
        {
            try
            {
                action0(ref _writeBuffer);
                for (int i = 0; i < count; i++)
                {
                    T buffer = new T();
                    action1(ref buffer, i);
                    action2(ref _writeBuffer, ref buffer);
                }

                lock (_writeBuffer)
                {
                    // Swap
                    CoreUtils.Swap(ref _readBuffer, ref _writeBuffer);
                    
                    _updating = false;
                }
            }
            catch (Exception e)
            {
                _updating = false;
                throw;
            }
        });
    }
}