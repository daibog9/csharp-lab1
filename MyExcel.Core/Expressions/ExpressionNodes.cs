namespace MyExcel.Core.Expressions
{
	public enum UnaryOperator
	{
		Plus,
		Minus,
		Increment,
		Decrement
	}

	public enum BinaryOperator
	{
		Add,
		Subtract,
		Multiply,
		Divide,
		Modulo,
		IntegerDivide
	}

	public interface ICellValueProvider
	{
		Rational GetValue(CellAddress address);
	}

	public abstract class ExpressionNode
	{
		protected const int LeafDepth = 1;

		protected ExpressionNode(int depth)
		{
			Depth = depth;
		}

		public int Depth { get; }

		public abstract Rational Evaluate(ICellValueProvider cells);

		public abstract void CollectReferences(ICollection<CellAddress> references);
	}

	public sealed class NumberNode : ExpressionNode
	{
		public NumberNode(Rational value)
			: base(LeafDepth)
		{
			Value = value;
		}

		public Rational Value { get; }

		public override Rational Evaluate(ICellValueProvider cells)
		{
			return Value;
		}

		public override void CollectReferences(ICollection<CellAddress> references)
		{
		}
	}

	public sealed class CellReferenceNode : ExpressionNode
	{
		public CellReferenceNode(CellAddress address)
			: base(LeafDepth)
		{
			Address = address;
		}

		public CellAddress Address { get; }

		public override Rational Evaluate(ICellValueProvider cells)
		{
			return cells.GetValue(Address);
		}

		public override void CollectReferences(ICollection<CellAddress> references)
		{
			references.Add(Address);
		}
	}

	public sealed class UnaryOperationNode : ExpressionNode
	{
		public UnaryOperationNode(UnaryOperator op, ExpressionNode operand)
			: base(operand.Depth + 1)
		{
			Operator = op;
			Operand = operand;
		}

		public UnaryOperator Operator { get; }

		public ExpressionNode Operand { get; }

		public override Rational Evaluate(ICellValueProvider cells)
		{
			Rational value = Operand.Evaluate(cells);

			switch (Operator)
			{
				case UnaryOperator.Plus:
					return value;
				case UnaryOperator.Minus:
					return -value;
				case UnaryOperator.Increment:
					return value + Rational.One;
				case UnaryOperator.Decrement:
					return value - Rational.One;
				default:
					throw new InvalidOperationException("Невідома унарна операція.");
			}
		}

		public override void CollectReferences(ICollection<CellAddress> references)
		{
			Operand.CollectReferences(references);
		}
	}

	public sealed class BinaryOperationNode : ExpressionNode
	{
		public BinaryOperationNode(BinaryOperator op, ExpressionNode left, ExpressionNode right)
			: base(Math.Max(left.Depth, right.Depth) + 1)
		{
			Operator = op;
			Left = left;
			Right = right;
		}

		public BinaryOperator Operator { get; }

		public ExpressionNode Left { get; }

		public ExpressionNode Right { get; }

		public override Rational Evaluate(ICellValueProvider cells)
		{
			Rational left = Left.Evaluate(cells);
			Rational right = Right.Evaluate(cells);

			switch (Operator)
			{
				case BinaryOperator.Add:
					return left + right;
				case BinaryOperator.Subtract:
					return left - right;
				case BinaryOperator.Multiply:
					return left * right;
				case BinaryOperator.Divide:
					EnsureNotZero(right);
					return left / right;
				case BinaryOperator.Modulo:
					EnsureIntegers(left, right, "mod");
					EnsureNotZero(right);
					return Rational.Remainder(left, right);
				case BinaryOperator.IntegerDivide:
					EnsureIntegers(left, right, "div");
					EnsureNotZero(right);
					return Rational.IntegerDivide(left, right);
				default:
					throw new InvalidOperationException("Невідома бінарна операція.");
			}
		}

		public override void CollectReferences(ICollection<CellAddress> references)
		{
			Left.CollectReferences(references);
			Right.CollectReferences(references);
		}

		private static void EnsureNotZero(Rational divisor)
		{
			if (divisor.IsZero)
			{
				throw new EvaluationException("ділення на нуль");
			}
		}

		private static void EnsureIntegers(Rational left, Rational right, string operationName)
		{
			if (!left.IsInteger || !right.IsInteger)
			{
				throw new EvaluationException($"операція {operationName} визначена лише для цілих чисел");
			}
		}
	}
}
