using System.Runtime.InteropServices;
using System.Windows.Media;
using Windows.Graphics;
using WinRT;

namespace Dial.Services;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class CompositionGeometrySource : CompositionGeometrySource.IGeometrySourceAbi,
    CompositionGeometrySource.IInspectableAbi, CompositionGeometrySource.IGeometrySourceInterop, IDisposable
{
    private IntPtr _factory;
    private IntPtr _geometry;

    public CompositionGeometrySource(Geometry geometry)
    {
        bool initialized = false;
        try
        {
            Guid id = new("06152247-6f50-465a-9245-118bfd3b6007");
            Marshal.ThrowExceptionForHR(D2D1CreateFactory(1, ref id, IntPtr.Zero, out _factory));
            Marshal.ThrowExceptionForHR(Method<CreatePathGeometry>(_factory, 10)(_factory, out _geometry));
            Marshal.ThrowExceptionForHR(Method<OpenGeometry>(_geometry, 17)(_geometry, out IntPtr sink));
            try
            {
                var flattened = geometry.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);
                Method<SetFillMode>(sink, 3)(sink, flattened.FillRule == FillRule.Nonzero ? 1 : 0);
                foreach (var figure in flattened.Figures)
                {
                    Method<BeginFigure>(sink, 5)(sink, new FloatPoint(figure.StartPoint), 0);
                    var points = figure.Segments.SelectMany(segment => segment switch
                    {
                        PolyLineSegment lines => lines.Points.AsEnumerable(),
                        LineSegment line => [line.Point],
                        _ => throw new InvalidOperationException("The composition clip contains a curved segment.")
                    }).Select(point => new FloatPoint(point)).ToArray();
                    Method<AddLines>(sink, 6)(sink, points, (uint)points.Length);
                    Method<EndFigure>(sink, 8)(sink, 1);
                }
                Marshal.ThrowExceptionForHR(Method<CloseGeometry>(sink, 9)(sink));
            }
            finally { Marshal.Release(sink); }
            initialized = true;
        }
        finally
        {
            if (!initialized) Dispose();
        }
    }

    public IGeometrySource2D AsGeometry()
    {
        IntPtr source = Marshal.GetComInterfaceForObject(this, typeof(IGeometrySourceAbi));
        try { return MarshalInterface<IGeometrySource2D>.FromAbi(source); }
        finally { Marshal.Release(source); }
    }

    public void GetGeometry(out IntPtr geometry)
    {
        ObjectDisposedException.ThrowIf(_geometry == IntPtr.Zero, this);
        geometry = _geometry;
        Marshal.AddRef(geometry);
    }

    public void TryGetGeometryUsingFactory(IntPtr factory, out IntPtr geometry)
    {
        geometry = IntPtr.Zero;
        if (factory == _factory) GetGeometry(out geometry);
    }

    public int GetIids(out uint count, out IntPtr ids)
    {
        count = 1;
        ids = Marshal.AllocCoTaskMem(16);
        Marshal.StructureToPtr(typeof(IGeometrySourceAbi).GUID, ids, false);
        return 0;
    }

    public int GetRuntimeClassName(out IntPtr name) { name = IntPtr.Zero; return 0; }
    public int GetTrustLevel(out int trust) { trust = 0; return 0; }

    public void Dispose()
    {
        if (_geometry != IntPtr.Zero) { Marshal.Release(_geometry); _geometry = IntPtr.Zero; }
        if (_factory != IntPtr.Zero) { Marshal.Release(_factory); _factory = IntPtr.Zero; }
    }

    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    [ComVisible(true), Guid("caff7902-670c-4181-a624-da977203b845"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGeometrySourceAbi
    {
        [PreserveSig] int GetIids(out uint count, out IntPtr ids);
        [PreserveSig] int GetRuntimeClassName(out IntPtr name);
        [PreserveSig] int GetTrustLevel(out int trust);
    }

    [ComVisible(true), Guid("af86e2e0-b12d-4c6a-9c5a-d7aa65101e90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInspectableAbi
    {
        [PreserveSig] int GetIids(out uint count, out IntPtr ids);
        [PreserveSig] int GetRuntimeClassName(out IntPtr name);
        [PreserveSig] int GetTrustLevel(out int trust);
    }

    [ComVisible(true), Guid("0657af73-53fd-47cf-84ff-c8492d2a80a3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGeometrySourceInterop
    {
        void GetGeometry(out IntPtr geometry);
        void TryGetGeometryUsingFactory(IntPtr factory, out IntPtr geometry);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FloatPoint(System.Windows.Point point)
    {
        public readonly float X = (float)point.X;
        public readonly float Y = (float)point.Y;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreatePathGeometry(IntPtr instance, out IntPtr geometry);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int OpenGeometry(IntPtr instance, out IntPtr sink);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void SetFillMode(IntPtr instance, int mode);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void BeginFigure(IntPtr instance, FloatPoint start, int begin);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void AddLines(IntPtr instance, [In] FloatPoint[] points, uint count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void EndFigure(IntPtr instance, int end);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CloseGeometry(IntPtr instance);
    [DllImport("d2d1.dll")]
    private static extern int D2D1CreateFactory(int type, ref Guid id, IntPtr options, out IntPtr factory);
}
