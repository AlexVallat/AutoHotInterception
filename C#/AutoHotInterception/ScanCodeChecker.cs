﻿using AutoHotInterception.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AutoHotInterception
{
	/*
     * Tool to check Scan Codes and Press / Release states
     */
	public class ScanCodeChecker: IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        private readonly Thread _thread;
        private readonly IntPtr _deviceContext;
        private int _filteredDevice;
        private dynamic _callback;

        public ScanCodeChecker()
        {
            _deviceContext = ManagedWrapper.CreateContext();
            _thread = new Thread(ThreadWorker);
        }

        public void Subscribe(int vid, int pid, dynamic callback)
        {
            _callback = callback;
            _filteredDevice = HelperFunctions.GetDeviceId(_deviceContext, false, vid, pid, 1);
            if (_filteredDevice == 0)
            {
                throw new Exception($"Could not find device with VID {vid}, PID {pid}");
            }

            ManagedWrapper.SetFilter(_deviceContext, IsMonitoredDevice, ManagedWrapper.Filter.All);
            _thread.Start();
        }

        public string OkCheck()
        {
            return "OK";
        }

        private int IsMonitoredDevice(int device)
        {
            return Convert.ToInt32(_filteredDevice == device);
        }

        public void ThreadWorker(object state)
        {
            var stroke = new ManagedWrapper.Stroke();
            while (!_cancellation.IsCancellationRequested)
            {
                //var device = ManagedWrapper.WaitWithTimeout(_deviceContext, (ulong)MaxWaitTime.TotalMilliseconds);
                var device = CancellableWait.Wait(_deviceContext, _cancellation.Token);
                if (device > 0)
                {
                    var keyEvents = new List<KeyEvent>();
                    while (ManagedWrapper.Receive(_deviceContext, device, ref stroke, 1) > 0)
                    {
                        keyEvents.Add(new KeyEvent { Code = stroke.key.code, State = stroke.key.state });
                        ManagedWrapper.Send(_deviceContext, _filteredDevice, ref stroke, 1);
                    }

                    if (keyEvents.Count > 0)
                    {
                        _callback(keyEvents.ToArray());
                    }
                }
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _thread.Join();
            _cancellation.Dispose();
            ManagedWrapper.DestroyContext(_deviceContext);
        }
    }

    public class KeyEvent
    {
        public ushort Code { get; set; }
        public ushort State { get; set; }
    }
}
