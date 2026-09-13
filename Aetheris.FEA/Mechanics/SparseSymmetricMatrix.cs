namespace Aetheris.FEA.Mechanics;

/// <summary>Minimal deterministic compressed-row sparse matrix for conventional PCG.</summary>
public sealed class SparseSymmetricMatrix
{
    private readonly SortedDictionary<int, double>[] rows;

    public SparseSymmetricMatrix(int size)
    {
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
        rows = Enumerable.Range(0, size).Select(_ => new SortedDictionary<int, double>()).ToArray();
    }

    public int Size { get; }
    public int Nonzeros => rows.Sum(row => row.Count);
    public IReadOnlyList<SortedDictionary<int, double>> Rows => rows;
    public double this[int row, int column] => rows[row].TryGetValue(column, out var value) ? value : 0;

    public void Add(int row, int column, double value)
    {
        if (!double.IsFinite(value)) throw new ArithmeticException("Sparse assembly received a non-finite value.");
        if (double.Abs(value) < 1e-30) return;
        rows[row][column] = rows[row].TryGetValue(column, out var current) ? current + value : value;
    }

    public double[] Multiply(IReadOnlyList<double> vector)
    {
        var result = new double[Size];
        for (var row = 0; row < Size; row++)
            foreach (var item in rows[row]) result[row] += item.Value * vector[item.Key];
        return result;
    }

    public double MaximumAsymmetry()
    {
        var maximum = 0d;
        for (var row = 0; row < Size; row++)
            foreach (var item in rows[row]) maximum = double.Max(maximum, double.Abs(item.Value - this[item.Key, row]));
        return maximum;
    }

    public bool IsFinite() => rows.All(row => row.Values.All(double.IsFinite));

    public (double MinimumDiagonal,double MaximumDiagonal,double DiagonalRatio,double MinimumRowNorm,double MaximumRowNorm,double RowNormRatio) ConditioningProxy()
    {
        var diagonals=Enumerable.Range(0,Size).Select(i=>double.Abs(this[i,i])).Where(value=>value>0).ToArray();
        var norms=rows.Select(row=>double.Sqrt(row.Values.Sum(value=>value*value))).Where(value=>value>0).ToArray();
        var minD=diagonals.Length==0?0:diagonals.Min();var maxD=diagonals.Length==0?0:diagonals.Max();var minR=norms.Length==0?0:norms.Min();var maxR=norms.Length==0?0:norms.Max();
        return(minD,maxD,minD==0?double.PositiveInfinity:maxD/minD,minR,maxR,minR==0?double.PositiveInfinity:maxR/minR);
    }

    public bool IsPositiveDefiniteByDenseCholesky(double relativePivotTolerance=1e-12)
    {
        var lower=new double[Size,Size];var scale=Enumerable.Range(0,Size).Max(i=>double.Abs(this[i,i]));
        for(var i=0;i<Size;i++)for(var j=0;j<=i;j++)
        {
            var sum=this[i,j];for(var k=0;k<j;k++)sum-=lower[i,k]*lower[j,k];
            if(i==j){if(!double.IsFinite(sum)||sum<=scale*relativePivotTolerance)return false;lower[i,j]=double.Sqrt(sum);}
            else lower[i,j]=sum/lower[j,j];
        }
        return true;
    }

    public SparseSymmetricMatrix Copy()
    {
        var copy = new SparseSymmetricMatrix(Size);
        for (var row = 0; row < Size; row++)
            foreach (var item in rows[row]) copy.rows[row][item.Key] = item.Value;
        return copy;
    }

    public void ApplyDirichlet(IReadOnlyDictionary<int, double> prescribed, double[] load)
    {
        foreach (var pair in prescribed.OrderBy(item => item.Key))
        {
            var dof = pair.Key;
            var value = pair.Value;
            for (var row = 0; row < Size; row++)
            {
                if (row == dof) continue;
                if (rows[row].Remove(dof, out var coefficient)) load[row] -= coefficient * value;
            }
            rows[dof].Clear();
            rows[dof][dof] = 1;
            load[dof] = value;
        }
    }
}

public static class PreconditionedConjugateGradient
{
    public static (double[] Solution, SolverConvergence Convergence) Solve(SparseSymmetricMatrix matrix, double[] rhs, double relativeTolerance = 1e-9, int? maximumIterations = null)
        =>Solve(matrix,rhs,new JacobiPreconditioner(matrix),relativeTolerance,maximumIterations);

    public static (double[] Solution, SolverConvergence Convergence) Solve(SparseSymmetricMatrix matrix,double[] rhs,ISpdPreconditioner preconditioner,double relativeTolerance=1e-9,int? maximumIterations=null)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var n = matrix.Size;
        var max = maximumIterations ?? double.Max(100, 4 * n);
        var x = new double[n];
        var r = (double[])rhs.Clone();
        var initial = Norm(r);
        var history = new List<double> { initial };
        if (initial == 0) return (x, new(true, 0, 0, 0, history, System.Diagnostics.Stopwatch.GetElapsedTime(started)));
        var z = preconditioner.Apply(r);
        var preconditionedHistory=new List<double>{double.Sqrt(double.Max(0,Dot(r,z)))};
        var p = (double[])z.Clone();
        var rz = Dot(r, z);
        var converged = false;
        var iteration = 0;
        for (; iteration < max; iteration++)
        {
            var ap = matrix.Multiply(p);
            var denominator = Dot(p, ap);
            if (!double.IsFinite(denominator) || denominator <= 0) break;
            var alpha = rz / denominator;
            for (var i = 0; i < n; i++) { x[i] += alpha * p[i]; r[i] -= alpha * ap[i]; }
            var residual = Norm(r);
            history.Add(residual);
            z=preconditioner.Apply(r);
            preconditionedHistory.Add(double.Sqrt(double.Max(0,Dot(r,z))));
            if (residual <= relativeTolerance * initial) { converged = true; iteration++; break; }
            var nextRz = Dot(r, z);
            var beta = nextRz / rz;
            for (var i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
            rz = nextRz;
        }
        return (x, new(converged, iteration, initial, history[^1], history, System.Diagnostics.Stopwatch.GetElapsedTime(started),preconditionedHistory));
    }

    private static double Dot(IReadOnlyList<double> a, IReadOnlyList<double> b) { var sum = 0d; for (var i = 0; i < a.Count; i++) sum += a[i] * b[i]; return sum; }
    private static double Norm(IReadOnlyList<double> a) => double.Sqrt(Dot(a, a));
}

public interface ISpdPreconditioner
{
    string Name { get; }
    double[] Apply(IReadOnlyList<double> residual);
}

public sealed class IdentityPreconditioner(int size):ISpdPreconditioner
{
    public string Name=>"Identity";
    public double[] Apply(IReadOnlyList<double> residual){if(residual.Count!=size)throw new ArgumentException("Residual length does not match preconditioner.",nameof(residual));return residual.ToArray();}
}

public sealed class JacobiPreconditioner:ISpdPreconditioner
{
    private readonly double[] inverseDiagonal;
    public JacobiPreconditioner(SparseSymmetricMatrix matrix)
    {
        inverseDiagonal=new double[matrix.Size];
        for(var i=0;i<matrix.Size;i++){var diagonal=matrix[i,i];if(!double.IsFinite(diagonal)||diagonal<=0)throw new InvalidOperationException($"Jacobi requires a finite positive diagonal at row {i}; got {diagonal:R}.");inverseDiagonal[i]=1/diagonal;}
    }
    public string Name=>"Jacobi";
    public double[] Apply(IReadOnlyList<double> residual){var result=new double[inverseDiagonal.Length];for(var i=0;i<result.Length;i++)result[i]=residual[i]*inverseDiagonal[i];return result;}
}

public sealed class BlockJacobi3Preconditioner:ISpdPreconditioner
{
    private readonly double[][] lower;
    public BlockJacobi3Preconditioner(SparseSymmetricMatrix matrix)
    {
        if(matrix.Size%3!=0)throw new InvalidOperationException("3x3 block Jacobi requires three displacement components per geometric basis mode.");lower=new double[matrix.Size/3][];
        for(var block=0;block<lower.Length;block++)
        {
            var i=3*block;var l00=Root(matrix[i,i],block);var l10=matrix[i+1,i]/l00;var l20=matrix[i+2,i]/l00;var l11=Root(matrix[i+1,i+1]-l10*l10,block);var l21=(matrix[i+2,i+1]-l20*l10)/l11;var l22=Root(matrix[i+2,i+2]-l20*l20-l21*l21,block);lower[block]=[l00,l10,l20,l11,l21,l22];
        }
    }
    public string Name=>"BlockJacobi3";
    public double[] Apply(IReadOnlyList<double> residual)
    {
        var result=new double[residual.Count];for(var block=0;block<lower.Length;block++)
        {
            var i=3*block;var l=lower[block];var y0=residual[i]/l[0];var y1=(residual[i+1]-l[1]*y0)/l[3];var y2=(residual[i+2]-l[2]*y0-l[4]*y1)/l[5];result[i+2]=y2/l[5];result[i+1]=(y1-l[4]*result[i+2])/l[3];result[i]=(y0-l[1]*result[i+1]-l[2]*result[i+2])/l[0];
        }return result;
    }
    private static double Root(double value,int block){if(!double.IsFinite(value)||value<=0)throw new InvalidOperationException($"3x3 block Jacobi encountered a nonpositive pivot in block {block}: {value:R}.");return double.Sqrt(value);}
}

public sealed class IncompleteCholeskyZeroPreconditioner:ISpdPreconditioner
{
    private readonly SortedDictionary<int,double>[] lower;
    private readonly List<(int Row,double Value)>[] columnEntries;
    public IncompleteCholeskyZeroPreconditioner(SparseSymmetricMatrix matrix,double diagonalShift=0)
    {
        if(!double.IsFinite(diagonalShift)||diagonalShift<0)throw new ArgumentOutOfRangeException(nameof(diagonalShift));
        lower=Enumerable.Range(0,matrix.Size).Select(_=>new SortedDictionary<int,double>()).ToArray();
        for(var i=0;i<matrix.Size;i++)
        {
            foreach(var j in matrix.Rows[i].Keys.Where(j=>j<i))
            {
                var sum=matrix[i,j];
                foreach(var left in lower[i].Where(item=>item.Key<j))if(lower[j].TryGetValue(left.Key,out var right))sum-=left.Value*right;
                var pivot=lower[j].GetValueOrDefault(j);if(!double.IsFinite(pivot)||pivot<=0)throw new InvalidOperationException($"IC(0) encountered an invalid pivot at row {j}.");
                lower[i][j]=sum/pivot;
            }
            var diagonal=matrix[i,i]+diagonalShift;
            foreach(var value in lower[i].Values)diagonal-=value*value;
            if(!double.IsFinite(diagonal)||diagonal<=0)throw new InvalidOperationException($"IC(0) encountered a nonpositive pivot at row {i}: {diagonal:R}.");
            lower[i][i]=double.Sqrt(diagonal);
        }
        columnEntries=Enumerable.Range(0,matrix.Size).Select(_=>new List<(int,double)>()).ToArray();
        for(var row=0;row<matrix.Size;row++)foreach(var item in lower[row])if(item.Key<row)columnEntries[item.Key].Add((row,item.Value));
        DiagonalShift=diagonalShift;
    }
    public string Name=>"IC(0)";
    public double DiagonalShift { get; }
    public double[] Apply(IReadOnlyList<double> residual)
    {
        var y=new double[lower.Length];
        for(var i=0;i<y.Length;i++){var value=residual[i];foreach(var item in lower[i]){if(item.Key>=i)break;value-=item.Value*y[item.Key];}y[i]=value/lower[i][i];}
        var result=new double[lower.Length];
        for(var i=result.Length-1;i>=0;i--){var value=y[i];foreach(var item in columnEntries[i])value-=item.Value*result[item.Row];result[i]=value/lower[i][i];}
        return result;
    }
}

public sealed record SymmetricDiagonalScaling(SparseSymmetricMatrix Matrix,double[] RightHandSide,double[] Scale,double MinimumScale,double MaximumScale)
{
    public static SymmetricDiagonalScaling Create(SparseSymmetricMatrix source,IReadOnlyList<double> rhs)
    {
        var scale=new double[source.Size];
        for(var i=0;i<source.Size;i++){var diagonal=source[i,i];if(!double.IsFinite(diagonal)||diagonal<=0)throw new InvalidOperationException($"Symmetric equilibration requires a finite positive diagonal at row {i}; got {diagonal:R}.");scale[i]=1/double.Sqrt(diagonal);}
        var matrix=new SparseSymmetricMatrix(source.Size);
        for(var row=0;row<source.Size;row++)foreach(var item in source.Rows[row])matrix.Add(row,item.Key,item.Value*scale[row]*scale[item.Key]);
        var load=new double[source.Size];for(var i=0;i<load.Length;i++)load[i]=rhs[i]*scale[i];
        return new(matrix,load,scale,scale.Min(),scale.Max());
    }
    public double[] Recover(IReadOnlyList<double> scaledSolution){var result=new double[Scale.Length];for(var i=0;i<result.Length;i++)result[i]=Scale[i]*scaledSolution[i];return result;}
}
