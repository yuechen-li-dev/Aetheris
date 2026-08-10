using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Math;

/// <summary>
/// Deterministic double-precision affine transform using the historical
/// System.Numerics row-vector composition convention. Therefore
/// <c>first * second</c> applies <c>first</c>, then <c>second</c>.
/// </summary>
public readonly struct Transform3D
{
    private readonly double _m11, _m12, _m13, _m14;
    private readonly double _m21, _m22, _m23, _m24;
    private readonly double _m31, _m32, _m33, _m34;
    private readonly double _m41, _m42, _m43, _m44;

    private Transform3D(double[] m)
    {
        _m11=m[0]; _m12=m[1]; _m13=m[2]; _m14=m[3];
        _m21=m[4]; _m22=m[5]; _m23=m[6]; _m24=m[7];
        _m31=m[8]; _m32=m[9]; _m33=m[10]; _m34=m[11];
        _m41=m[12]; _m42=m[13]; _m43=m[14]; _m44=m[15];
    }

    public static Transform3D Identity { get; } = new([1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1]);

    public static Transform3D FromRowMajor(IReadOnlyList<double> matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (matrix.Count != 16 || matrix.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("A finite 4x4 row-major matrix is required.", nameof(matrix));
        var transform = new Transform3D(matrix.ToArray());
        if (!transform.IsRigid()) throw new ArgumentException("Assembly instance transforms must be rigid.", nameof(matrix));
        return transform;
    }

    public static Transform3D CreateTranslation(Vector3D t) =>
        new([1,0,0,0, 0,1,0,0, 0,0,1,0, t.X,t.Y,t.Z,1]);

    public static Transform3D CreateScale(double scale) => CreateScale(new Vector3D(scale, scale, scale));

    public static Transform3D CreateScale(Vector3D s) =>
        new([s.X,0,0,0, 0,s.Y,0,0, 0,0,s.Z,0, 0,0,0,1]);

    public static Transform3D CreateRotationX(double radians)
    {
        var c=double.Cos(radians); var s=double.Sin(radians);
        return new([1,0,0,0, 0,c,s,0, 0,-s,c,0, 0,0,0,1]);
    }

    public static Transform3D CreateRotationY(double radians)
    {
        var c=double.Cos(radians); var s=double.Sin(radians);
        return new([c,0,-s,0, 0,1,0,0, s,0,c,0, 0,0,0,1]);
    }

    public static Transform3D CreateRotationZ(double radians)
    {
        var c=double.Cos(radians); var s=double.Sin(radians);
        return new([c,s,0,0, -s,c,0,0, 0,0,1,0, 0,0,0,1]);
    }

    public static Transform3D operator *(Transform3D left, Transform3D right)
    {
        var a=left.ToArray(); var b=right.ToArray(); var result=new double[16];
        for(var row=0;row<4;row++) for(var column=0;column<4;column++)
            for(var k=0;k<4;k++) result[(row*4)+column]+=a[(row*4)+k]*b[(k*4)+column];
        return new(result);
    }

    public static Transform3D Compose(Transform3D first, Transform3D second) => first * second;

    public bool TryInverse(out Transform3D inverse)
    {
        var augmented=new double[4,8]; var source=ToArray();
        for(var row=0;row<4;row++) for(var column=0;column<4;column++)
        { augmented[row,column]=source[(row*4)+column]; augmented[row,column+4]=row==column?1d:0d; }
        for(var column=0;column<4;column++)
        {
            var pivot=column;
            for(var row=column+1;row<4;row++) if(double.Abs(augmented[row,column])>double.Abs(augmented[pivot,column])) pivot=row;
            if(double.Abs(augmented[pivot,column])<=1e-300d) { inverse=default; return false; }
            if(pivot!=column) for(var j=0;j<8;j++) (augmented[column,j],augmented[pivot,j])=(augmented[pivot,j],augmented[column,j]);
            var divisor=augmented[column,column]; for(var j=0;j<8;j++) augmented[column,j]/=divisor;
            for(var row=0;row<4;row++) if(row!=column)
            { var factor=augmented[row,column]; for(var j=0;j<8;j++) augmented[row,j]-=factor*augmented[column,j]; }
        }
        var values=new double[16]; for(var row=0;row<4;row++) for(var column=0;column<4;column++) values[(row*4)+column]=augmented[row,column+4];
        inverse=new(values); return true;
    }

    public Transform3D Inverse() => TryInverse(out var inverse)
        ? inverse : throw new InvalidOperationException("Transform is singular and cannot be inverted.");

    public Point3D Apply(Point3D p)
    {
        var x=(p.X*_m11)+(p.Y*_m21)+(p.Z*_m31)+_m41;
        var y=(p.X*_m12)+(p.Y*_m22)+(p.Z*_m32)+_m42;
        var z=(p.X*_m13)+(p.Y*_m23)+(p.Z*_m33)+_m43;
        var w=(p.X*_m14)+(p.Y*_m24)+(p.Z*_m34)+_m44;
        return w==1d ? new(x,y,z) : new(x/w,y/w,z/w);
    }

    public Vector3D Apply(Vector3D v) => new(
        (v.X*_m11)+(v.Y*_m21)+(v.Z*_m31),
        (v.X*_m12)+(v.Y*_m22)+(v.Z*_m32),
        (v.X*_m13)+(v.Y*_m23)+(v.Z*_m33));

    public Direction3D Apply(Direction3D direction, ToleranceContext? toleranceContext = null)
    {
        if (!Direction3D.TryCreate(Apply(direction.ToVector()), out var result, toleranceContext))
            throw new InvalidOperationException("Transform collapses direction to near-zero length.");
        return result;
    }

    /// <summary>True when the linear part preserves Euclidean lengths within the supplied tolerance.</summary>
    public bool IsRigid(double tolerance = 1e-12d)
    {
        var x=Apply(new Vector3D(1,0,0)); var y=Apply(new Vector3D(0,1,0)); var z=Apply(new Vector3D(0,0,1));
        return double.Abs(x.Length-1)<=tolerance && double.Abs(y.Length-1)<=tolerance && double.Abs(z.Length-1)<=tolerance
            && double.Abs(x.Dot(y))<=tolerance && double.Abs(x.Dot(z))<=tolerance && double.Abs(y.Dot(z))<=tolerance;
    }

    private double[] ToArray() => [_m11,_m12,_m13,_m14, _m21,_m22,_m23,_m24, _m31,_m32,_m33,_m34, _m41,_m42,_m43,_m44];
}
