using System;
using System.Text;

namespace LowObservable.Core.Maths
{
    /// <summary>
    /// Small dense row-major matrix of doubles. Sized for estimation work (3x3 poses today,
    /// a few dozen states for EKF-SLAM later), so it favours clarity over speed. Operations return
    /// new matrices; nothing mutates in place except the explicit indexer.
    /// </summary>
    public sealed class Matrix
    {
        private readonly double[] _data;

        public int Rows { get; }
        public int Cols { get; }

        public Matrix(int rows, int cols)
        {
            if (rows <= 0 || cols <= 0) throw new ArgumentOutOfRangeException(nameof(rows), "Matrix dimensions must be positive.");
            Rows = rows;
            Cols = cols;
            _data = new double[rows * cols];
        }

        public Matrix(int rows, int cols, params double[] rowMajorValues) : this(rows, cols)
        {
            if (rowMajorValues.Length != rows * cols)
                throw new ArgumentException($"Expected {rows * cols} values, got {rowMajorValues.Length}.");
            Array.Copy(rowMajorValues, _data, _data.Length);
        }

        public double this[int r, int c]
        {
            get => _data[r * Cols + c];
            set => _data[r * Cols + c] = value;
        }

        public static Matrix Identity(int n)
        {
            var m = new Matrix(n, n);
            for (int i = 0; i < n; i++) m[i, i] = 1.0;
            return m;
        }

        public static Matrix Diagonal(params double[] values)
        {
            var m = new Matrix(values.Length, values.Length);
            for (int i = 0; i < values.Length; i++) m[i, i] = values[i];
            return m;
        }

        public static Matrix ColumnVector(params double[] values) => new Matrix(values.Length, 1, values);

        public Matrix Clone()
        {
            var m = new Matrix(Rows, Cols);
            Array.Copy(_data, m._data, _data.Length);
            return m;
        }

        public Matrix Transpose()
        {
            var t = new Matrix(Cols, Rows);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    t[c, r] = this[r, c];
            return t;
        }

        public static Matrix operator +(Matrix a, Matrix b)
        {
            RequireSameShape(a, b);
            var m = new Matrix(a.Rows, a.Cols);
            for (int i = 0; i < a._data.Length; i++) m._data[i] = a._data[i] + b._data[i];
            return m;
        }

        public static Matrix operator -(Matrix a, Matrix b)
        {
            RequireSameShape(a, b);
            var m = new Matrix(a.Rows, a.Cols);
            for (int i = 0; i < a._data.Length; i++) m._data[i] = a._data[i] - b._data[i];
            return m;
        }

        public static Matrix operator *(Matrix a, double s)
        {
            var m = new Matrix(a.Rows, a.Cols);
            for (int i = 0; i < a._data.Length; i++) m._data[i] = a._data[i] * s;
            return m;
        }

        public static Matrix operator *(double s, Matrix a) => a * s;

        public static Matrix operator *(Matrix a, Matrix b)
        {
            if (a.Cols != b.Rows) throw new ArgumentException($"Cannot multiply {a.Rows}x{a.Cols} by {b.Rows}x{b.Cols}.");
            var m = new Matrix(a.Rows, b.Cols);
            for (int r = 0; r < a.Rows; r++)
            {
                for (int k = 0; k < a.Cols; k++)
                {
                    double v = a[r, k];
                    if (v == 0.0) continue;
                    for (int c = 0; c < b.Cols; c++) m[r, c] += v * b[k, c];
                }
            }
            return m;
        }

        /// <summary>Returns (A + A^T) / 2. Covariances drift from symmetry through round-off; call this after updates.</summary>
        public Matrix Symmetrized()
        {
            if (Rows != Cols) throw new InvalidOperationException("Only square matrices can be symmetrized.");
            var m = new Matrix(Rows, Cols);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    m[r, c] = 0.5 * (this[r, c] + this[c, r]);
            return m;
        }

        public double Trace()
        {
            double t = 0;
            for (int i = 0; i < System.Math.Min(Rows, Cols); i++) t += this[i, i];
            return t;
        }

        /// <summary>
        /// Lower-triangular Cholesky factor L with A = L L^T. Returns false if A is not (numerically)
        /// symmetric positive definite, which for a covariance means the filter has gone wrong.
        /// </summary>
        public bool TryCholesky(out Matrix lower)
        {
            if (Rows != Cols) throw new InvalidOperationException("Cholesky needs a square matrix.");
            int n = Rows;
            lower = new Matrix(n, n);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = this[i, j];
                    for (int k = 0; k < j; k++) sum -= lower[i, k] * lower[j, k];
                    if (i == j)
                    {
                        if (sum <= 0.0 || double.IsNaN(sum)) return false;
                        lower[i, i] = System.Math.Sqrt(sum);
                    }
                    else
                    {
                        lower[i, j] = sum / lower[j, j];
                    }
                }
            }
            return true;
        }

        /// <summary>Inverse by Gauss-Jordan elimination with partial pivoting.</summary>
        public Matrix Inverse()
        {
            if (Rows != Cols) throw new InvalidOperationException("Only square matrices can be inverted.");
            int n = Rows;
            var a = Clone();
            var inv = Identity(n);
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double best = System.Math.Abs(a[col, col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = System.Math.Abs(a[r, col]);
                    if (v > best) { best = v; pivot = r; }
                }
                if (best < 1e-15) throw new InvalidOperationException("Matrix is singular.");
                if (pivot != col) { a.SwapRows(col, pivot); inv.SwapRows(col, pivot); }

                double diag = a[col, col];
                for (int c = 0; c < n; c++) { a[col, c] /= diag; inv[col, c] /= diag; }

                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double f = a[r, col];
                    if (f == 0.0) continue;
                    for (int c = 0; c < n; c++)
                    {
                        a[r, c] -= f * a[col, c];
                        inv[r, c] -= f * inv[col, c];
                    }
                }
            }
            return inv;
        }

        /// <summary>Determinant via LU-style elimination. Used for Gaussian normalisation constants.</summary>
        public double Determinant()
        {
            if (Rows != Cols) throw new InvalidOperationException("Only square matrices have determinants.");
            int n = Rows;
            var a = Clone();
            double det = 1.0;
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double best = System.Math.Abs(a[col, col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = System.Math.Abs(a[r, col]);
                    if (v > best) { best = v; pivot = r; }
                }
                if (best < 1e-300) return 0.0;
                if (pivot != col) { a.SwapRows(col, pivot); det = -det; }
                det *= a[col, col];
                for (int r = col + 1; r < n; r++)
                {
                    double f = a[r, col] / a[col, col];
                    for (int c = col; c < n; c++) a[r, c] -= f * a[col, c];
                }
            }
            return det;
        }

        /// <summary>Quadratic form v^T A v for a column vector v. With A = S^-1 this is a squared Mahalanobis distance.</summary>
        public double QuadraticForm(Matrix v)
        {
            if (v.Cols != 1 || v.Rows != Rows || Rows != Cols) throw new ArgumentException("QuadraticForm needs a square matrix and a matching column vector.");
            double sum = 0;
            for (int r = 0; r < Rows; r++)
            {
                double row = 0;
                for (int c = 0; c < Cols; c++) row += this[r, c] * v[c, 0];
                sum += v[r, 0] * row;
            }
            return sum;
        }

        private void SwapRows(int a, int b)
        {
            for (int c = 0; c < Cols; c++)
            {
                double t = this[a, c];
                this[a, c] = this[b, c];
                this[b, c] = t;
            }
        }

        private static void RequireSameShape(Matrix a, Matrix b)
        {
            if (a.Rows != b.Rows || a.Cols != b.Cols)
                throw new ArgumentException($"Shape mismatch: {a.Rows}x{a.Cols} vs {b.Rows}x{b.Cols}.");
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < Rows; r++)
            {
                sb.Append(r == 0 ? "[" : " ");
                for (int c = 0; c < Cols; c++)
                {
                    sb.Append(this[r, c].ToString("0.0000"));
                    if (c < Cols - 1) sb.Append(", ");
                }
                sb.Append(r == Rows - 1 ? "]" : "\n");
            }
            return sb.ToString();
        }
    }
}
