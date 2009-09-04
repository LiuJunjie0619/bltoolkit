﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BLToolkit.Data.Sql.SqlProvider
{
	using DataProvider;

#if FW3
	using Linq;

	using C = Char;
	using S = String;
	using I = Int32;
#endif

	public class OracleSqlProvider : BasicSqlProvider
	{
		public OracleSqlProvider(DataProviderBase dataProvider) : base(dataProvider)
		{
		}

		protected override void BuildSelectClause(StringBuilder sb)
		{
			if (SqlBuilder.From.Tables.Count == 0)
			{
				AppendIndent(sb);
				sb.Append("SELECT").AppendLine();
				BuildColumns(sb);
				AppendIndent(sb);
				sb.Append("FROM SYS.DUAL").AppendLine();
			}
			else
				base.BuildSelectClause(sb);
		}

		protected override bool BuildWhere()
		{
			return (base.BuildWhere() || SqlBuilder.Select.TakeValue != null) && SqlBuilder.OrderBy.IsEmpty && SqlBuilder.Having.IsEmpty;
		}

		protected override void BuildSql(StringBuilder sb)
		{
			bool buildRowNum = SqlBuilder.Select.TakeValue != null && (!SqlBuilder.OrderBy.IsEmpty || !SqlBuilder.Having.IsEmpty);

			if (buildRowNum)
			{
				AppendIndent(sb).Append("SELECT * FROM (").AppendLine();
				Indent++;
			}

			base.BuildSql(sb);

			if (buildRowNum)
			{
				string alias = SqlBuilder.GetTempAliases(1, "t")[0];

				Indent--;

				AppendIndent(sb).Append(") ").Append(alias).AppendLine();
				AppendIndent(sb).Append("WHERE").AppendLine();

				Indent++;
				AppendIndent(sb).Append("rownum <= ");
				BuildExpression(sb, Precedence.Comparison, SqlBuilder.Select.TakeValue);
				sb.AppendLine();
				Indent--;
			}
		}

		protected override void BuildWhereSearchCondition(StringBuilder sb, SqlBuilder.SearchCondition condition)
		{
			if (SqlBuilder.Select.TakeValue != null && SqlBuilder.OrderBy.IsEmpty && SqlBuilder.Having.IsEmpty)
			{
				BuildPredicate(
					sb,
					Precedence.LogicalConjunction,
					new SqlBuilder.Predicate.ExprExpr(
						new SqlExpression("rownum", Precedence.Primary),
						SqlBuilder.Predicate.Operator.LessOrEqual,
						SqlBuilder.Select.TakeValue));

				if (base.BuildWhere())
				{
					sb.Append(" AND ");
					BuildSearchCondition(sb, Precedence.LogicalConjunction, condition);
				}
			}
			else
				BuildSearchCondition(sb, condition);
		}

		public override ISqlExpression ConvertExpression(ISqlExpression expr)
		{
			expr = base.ConvertExpression(expr);

			if (expr is SqlBinaryExpression)
			{
				SqlBinaryExpression be = (SqlBinaryExpression)expr;

				switch (be.Operation)
				{
					case "%": return new SqlFunction("MOD",    be.Expr1, be.Expr2);
					case "&": return new SqlFunction("BITAND", be.Expr1, be.Expr2);
					case "|": // (a + b) - BITAND(a, b)
						return Sub(
							Add(be.Expr1, be.Expr2, be.Type),
							new SqlFunction("BITAND", be.Expr1, be.Expr2),
							be.Type);

					case "^": // (a + b) - BITAND(a, b) * 2
						return Sub(
							Add(be.Expr1, be.Expr2, be.Type),
							Mul(new SqlFunction("BITAND", be.Expr1, be.Expr2), 2),
							be.Type);
					case "+": return be.Type == typeof(string)? new SqlBinaryExpression(be.Expr1, "||", be.Expr2, be.Type, be.Precedence): expr;
				}
			}
			else if (expr is SqlFunction)
			{
				SqlFunction func = (SqlFunction) expr;

				switch (func.Name)
				{
					case "Coalesce"  : return new SqlFunction("Nvl",    func.Parameters);
					case "Substring" : return new SqlFunction("Substr", func.Parameters);
					case "CharIndex" :
						return func.Parameters.Length == 2?
							new SqlFunction("InStr", func.Parameters[1], func.Parameters[0]):
							new SqlFunction("InStr", func.Parameters[1], func.Parameters[0], func.Parameters[2]);
				}
			}

			return expr;
		}

#if FW3
		protected override Dictionary<MemberInfo,BaseExpressor> GetExpressors() { return _members; }
		static    readonly Dictionary<MemberInfo,BaseExpressor> _members = new Dictionary<MemberInfo,BaseExpressor>
		{
			{ MI(() => Sql.Left ("",0)     ), new F<S,I,S>    ((p0,p1)       => Sql.Substring(p0, 1, p1)) },
			{ MI(() => Sql.Right("",0)     ), new F<S,I,S>    ((p0,p1)       => Sql.Substring(p0, p0.Length - p1 + 1, p1)) },
			{ MI(() => Sql.Stuff("",0,0,"")), new F<S,I,I,S,S>((p0,p1,p2,p3) => AltStuff(p0, p1, p2, p3)) },
			{ MI(() => Sql.Space(0)        ), new F<I,S>      ( p0           => Sql.PadRight(" ", p0, ' ')) },
		};
#endif
	}
}
