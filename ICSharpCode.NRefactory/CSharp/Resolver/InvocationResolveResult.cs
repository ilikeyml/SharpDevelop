﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;

using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;

namespace ICSharpCode.NRefactory.CSharp.Resolver
{
	/// <summary>
	/// Represents the result of a method invocation.
	/// </summary>
	public class InvocationResolveResult : MemberResolveResult
	{
		public readonly OverloadResolutionErrors OverloadResolutionErrors;
		public readonly IList<IType> TypeArguments;
		
		public readonly IList<ResolveResult> Arguments;
		
		/// <summary>
		/// Gets whether this invocation is calling an extension method using extension method syntax.
		/// </summary>
		public readonly bool IsExtensionMethodInvocation;
		
		/// <summary>
		/// Gets whether a params-Array is being used in its expanded form.
		/// </summary>
		public readonly bool IsExpandedForm;
		
		/// <summary>
		/// Gets whether this is a lifted operator invocation.
		/// </summary>
		public readonly bool IsLiftedOperatorInvocation;
		
		readonly IList<int> argumentToParameterMap;
		
		public InvocationResolveResult(ResolveResult targetResult, OverloadResolution or, ITypeResolveContext context)
			: base(
				or.IsExtensionMethodInvocation ? null : targetResult,
				or.BestCandidate,
				GetReturnType(or, context))
		{
			this.OverloadResolutionErrors = or.BestCandidateErrors;
			this.TypeArguments = or.InferredTypeArguments;
			this.argumentToParameterMap = or.GetArgumentToParameterMap();
			this.Arguments = or.GetArgumentsWithConversions();
			
			this.IsExtensionMethodInvocation = or.IsExtensionMethodInvocation;
			this.IsExpandedForm = or.BestCandidateIsExpandedForm;
			this.IsLiftedOperatorInvocation = or.BestCandidate is OverloadResolution.ILiftedOperator;
		}
		
		public InvocationResolveResult(
			ResolveResult targetResult, IParameterizedMember member, IType returnType,
			IList<ResolveResult> arguments,
			OverloadResolutionErrors overloadResolutionErrors = OverloadResolutionErrors.None,
			IList<IType> typeArguments = null,
			bool isExtensionMethodInvocation = false, bool isExpandedForm = false,
			bool isLiftedOperatorInvocation = false,
			IList<int> argumentToParameterMap = null)
			: base(targetResult, member, returnType)
		{
			this.OverloadResolutionErrors = overloadResolutionErrors;
			this.TypeArguments = typeArguments ?? EmptyList<IType>.Instance;
			this.Arguments = arguments ?? EmptyList<ResolveResult>.Instance;
			this.IsExtensionMethodInvocation = isExtensionMethodInvocation;
			this.IsExpandedForm = isExpandedForm;
			this.IsLiftedOperatorInvocation = isLiftedOperatorInvocation;
			this.argumentToParameterMap = argumentToParameterMap;
		}
		
		static IType GetReturnType(OverloadResolution or, ITypeResolveContext context)
		{
			if (context == null)
				throw new ArgumentNullException("context");
			
			IType returnType;
			if (or.BestCandidate.EntityType == EntityType.Constructor)
				returnType = or.BestCandidate.DeclaringType;
			else
				returnType = or.BestCandidate.ReturnType.Resolve(context);
			
			var typeArguments = or.InferredTypeArguments;
			if (typeArguments.Count > 0)
				return returnType.AcceptVisitor(new MethodTypeParameterSubstitution(typeArguments));
			else
				return returnType;
		}
		
		public override bool IsError {
			get { return this.OverloadResolutionErrors != OverloadResolutionErrors.None; }
		}
		
		/// <summary>
		/// Gets an array that maps argument indices to parameter indices.
		/// For arguments that could not be mapped to any parameter, the value will be -1.
		/// 
		/// parameterIndex = ArgumentToParameterMap[argumentIndex]
		/// </summary>
		public IList<int> GetArgumentToParameterMap()
		{
			return argumentToParameterMap;
		}
		
		public new IParameterizedMember Member {
			get { return (IParameterizedMember)base.Member; }
		}
		
		public override IEnumerable<ResolveResult> GetChildResults()
		{
			return base.GetChildResults().Concat(this.Arguments);
		}
	}
}
