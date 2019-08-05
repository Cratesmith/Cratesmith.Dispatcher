#if UNITY_5_6_OR_NEWER && (UNITY_EDITOR||(ENABLE_PLAYMODE_TESTS_RUNNER && PLAYMODE_TESTS) )
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace Plugins.Cratesmith.Dispatcher.Editor {

public class EventDispatcherTest
{
	[Test]
	public void PrimitiveTypes()
    {
        bool boolLogged = false;
        System.Action<bool> boolFunc = delegate(bool f)
        {
            boolLogged ^= true;
            Debug.LogFormat("bool: {0}",f);
        };

        bool intLogged = false;
        System.Action<int> intFunc = delegate(int f)
        {
            intLogged ^= true;
            Debug.LogFormat("int: {0}",f);
        };

        bool floatLogged = false;
        System.Action<float> floatFunc = delegate(float f)
        {
            floatLogged ^= true;
            Debug.LogFormat("float: {0}",f);
        };

        var dispatcher = new EventDispatcher();
        dispatcher.Add(boolFunc);
        dispatcher.Add(intFunc);
        dispatcher.Add(floatFunc);
        dispatcher.SendRaw(1);
        dispatcher.SendRaw(1.0f);
        dispatcher.SendRaw(true);

        dispatcher.Remove(boolFunc);
        dispatcher.Remove(intFunc);
        dispatcher.Remove(floatFunc);
        dispatcher.SendRaw(1);
        dispatcher.SendRaw(1.0f);
        dispatcher.SendRaw(true);

        Assert.IsTrue(boolLogged, "boolFunc wasn't triggered");
        Assert.IsTrue(intLogged, "intFunc wasn't triggered");
        Assert.IsTrue(floatLogged, "floatFunc wasn't triggered");
	}

    internal interface IInterfaceA : IDisposable        {}
    internal interface IInterfaceB : IDisposable		{}
    internal class BaseA : IInterfaceA                  {public void Dispose(){}}
    internal class SubclassA : BaseA                    {}
    internal class BaseB : IDisposable					{public void Dispose(){}}
    internal class SubclassB : BaseB, IInterfaceB      {}
    internal struct StructA : IInterfaceA               {public void Dispose(){}}
    internal struct StructC : IDisposable				{public void Dispose(){}}


    [Test]
	public void PooledInterfacesClassesAndStructs()
    {
        var typeCounts = new Dictionary<System.Type, int>();
        var dispatcher = new EventDispatcher();
            
        System.Func<System.Type, int> getTypeCount = (funcType) =>
        {
            if(!typeCounts.ContainsKey(funcType))
            {
                return 0;
            }
            return typeCounts[funcType];
        };

        System.Action<System.Type, System.Type> updateTypeCount = delegate(System.Type funcType, System.Type varType)
        {
			Profiler.BeginSample("UpdateTypeCount");
            if(!typeCounts.ContainsKey(funcType))
            {
                typeCounts[funcType] = 1;
            }
            else
            {
                ++typeCounts[funcType];
            }
            //Debug.LogFormat("{0}Func: {1}", funcType.Name, varType.Name);
			Profiler.EndSample();
        };

        System.Action<BaseA> baseAFunc = delegate(BaseA f) {updateTypeCount(typeof(BaseA), f.GetType());};
        dispatcher.Add(baseAFunc);

        System.Action<BaseB> baseBFunc = delegate(BaseB f) {updateTypeCount(typeof(BaseB), f.GetType());};
        dispatcher.Add(baseBFunc);

        System.Action<SubclassA> subAFunc = delegate(SubclassA f) {updateTypeCount(typeof(SubclassA), f.GetType());};
        dispatcher.Add(subAFunc);

        System.Action<SubclassB> subBFunc = delegate(SubclassB f) {updateTypeCount(typeof(SubclassB), f.GetType());};
        dispatcher.Add(subBFunc);

        System.Action<StructA> structAFunc = delegate(StructA f) {updateTypeCount(typeof(StructA), f.GetType());};
        dispatcher.Add(structAFunc);

        System.Action<StructC> structCFunc = delegate(StructC f) {updateTypeCount(typeof(StructC), f.GetType());};
        dispatcher.Add(structCFunc);

        System.Action<IInterfaceA> intAFunc = delegate(IInterfaceA f) {updateTypeCount(typeof(IInterfaceA), f.GetType());};
        dispatcher.Add(intAFunc);

        System.Action<IInterfaceB> intBFunc = delegate(IInterfaceB f) {updateTypeCount(typeof(IInterfaceB), f.GetType());};
        dispatcher.Add(intBFunc);

		// pre-send each type so they are pooled
	    using (dispatcher.SendScope<BaseA>()) {}
	    using (dispatcher.SendScope<SubclassA>()) {}
	    using (dispatcher.SendScope<BaseB>()) {}
	    using (dispatcher.SendScope<SubclassB>()) {}
	        
		typeCounts.Clear();
		Assert.That(() =>
		{
			using (dispatcher.SendScope<BaseA>())
			{
			}
		}, Is.Not.AllocatingGCMemory());

		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);
		
	    Assert.That(() =>
		{
			typeCounts.Clear();
			using (dispatcher.SendScope<SubclassA>()) {}			
	    }, Is.Not.AllocatingGCMemory());
	    Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
	    Assert.AreEqual(getTypeCount(typeof(BaseA)), 1);
	    Assert.AreEqual(getTypeCount(typeof(SubclassA)), 1);
	    Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
	    Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
	    Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
	    Assert.AreEqual(getTypeCount(typeof(StructC)), 0);
		
	    Assert.That(() =>
		{
			typeCounts.Clear();
			using (dispatcher.SendScope<BaseB>()) {}			
	    }, Is.Not.AllocatingGCMemory());
	    Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
	    Assert.AreEqual(getTypeCount(typeof(BaseB)), 1);
	    Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
	    Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

	    Assert.That(() =>
		{
			typeCounts.Clear();
			using (dispatcher.SendScope<SubclassB>()) {}			
	    }, Is.Not.AllocatingGCMemory());
	    Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
	    Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 1);
	    Assert.AreEqual(getTypeCount(typeof(BaseB)), 1);
	    Assert.AreEqual(getTypeCount(typeof(SubclassB)), 1);
	    Assert.AreEqual(getTypeCount(typeof(StructC)), 0);		
    }

	[Test]
	public void AddAndRemove()
	{
		var typeCounts = new Dictionary<System.Type, int>();
		var dispatcher = new EventDispatcher();
            
		System.Func<System.Type, int> getTypeCount = (funcType) =>
		{
			if(!typeCounts.ContainsKey(funcType))
			{
				return 0;
			}
			return typeCounts[funcType];
		};

		System.Action<System.Type, System.Type> updateTypeCount = delegate(System.Type funcType, System.Type varType)
		{
			if(!typeCounts.ContainsKey(funcType))
			{
				typeCounts[funcType] = 1;
			}
			else
			{
				++typeCounts[funcType];
			}
			Debug.LogFormat("{0}Func: {1}", funcType.Name, varType.Name);
		};

		System.Action<BaseA> baseAFunc = delegate(BaseA f) {updateTypeCount(typeof(BaseA), f.GetType());};
		dispatcher.SendRaw(new BaseA());
		dispatcher.Add(baseAFunc);
		dispatcher.Remove(baseAFunc);
		dispatcher.Add(baseAFunc);

		System.Action<BaseB> baseBFunc = delegate(BaseB f) {updateTypeCount(typeof(BaseB), f.GetType());};
		dispatcher.Add(baseBFunc);
		dispatcher.Remove(baseBFunc);
		dispatcher.Add(baseBFunc);


		System.Action<SubclassA> subAFunc = delegate(SubclassA f) {updateTypeCount(typeof(SubclassA), f.GetType());};
		dispatcher.SendRaw(new SubclassA());
		dispatcher.Add(subAFunc);
		dispatcher.Remove(subAFunc);
		dispatcher.Add(subAFunc);

		System.Action<SubclassB> subBFunc = delegate(SubclassB f) {updateTypeCount(typeof(SubclassB), f.GetType());};
		dispatcher.Add(subBFunc);
		dispatcher.Remove(subBFunc);
		dispatcher.Add(subBFunc);

		System.Action<StructA> structAFunc = delegate(StructA f) {updateTypeCount(typeof(StructA), f.GetType());};
		dispatcher.SendRaw(new StructA());
		dispatcher.Add(structAFunc);
		dispatcher.Remove(structAFunc);
		dispatcher.Add(structAFunc);

		System.Action<StructC> structCFunc = delegate(StructC f) {updateTypeCount(typeof(StructC), f.GetType());};
		dispatcher.Add(structCFunc);
		dispatcher.Remove(structCFunc);

		System.Action<IInterfaceA> intAFunc = delegate(IInterfaceA f) {updateTypeCount(typeof(IInterfaceA), f.GetType());};
		dispatcher.SendRaw(new StructA()); 
		dispatcher.Add(intAFunc);
		dispatcher.Remove(intAFunc);
		dispatcher.Add(intAFunc);

		System.Action<IInterfaceB> intBFunc = delegate(IInterfaceB f) {updateTypeCount(typeof(IInterfaceB), f.GetType());};
		dispatcher.Add(intBFunc);
		dispatcher.Remove(intBFunc);
		dispatcher.Add(intBFunc);
            
		typeCounts.Clear();
		dispatcher.SendRaw(new BaseA());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new SubclassA());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 1);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new StructA());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 1);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new BaseB());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new SubclassB());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 1);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new StructC());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);
	}

	[Test]
	public void InterfacesStructsAndClasses()
	{
		var typeCounts = new Dictionary<System.Type, int>();
		var dispatcher = new EventDispatcher();
            
		System.Func<System.Type, int> getTypeCount = (funcType) =>
		{
			if(!typeCounts.ContainsKey(funcType))
			{
				return 0;
			}
			return typeCounts[funcType];
		};

		System.Action<System.Type, System.Type> updateTypeCount = delegate(System.Type funcType, System.Type varType)
		{
			if(!typeCounts.ContainsKey(funcType))
			{
				typeCounts[funcType] = 1;
			}
			else
			{
				++typeCounts[funcType];
			}
			Debug.LogFormat("{0}Func: {1}", funcType.Name, varType.Name);
		};

		System.Action<BaseA> baseAFunc = delegate(BaseA f) {updateTypeCount(typeof(BaseA), f.GetType());};
		dispatcher.Add(baseAFunc);

		System.Action<BaseB> baseBFunc = delegate(BaseB f) {updateTypeCount(typeof(BaseB), f.GetType());};
		dispatcher.Add(baseBFunc);

		System.Action<SubclassA> subAFunc = delegate(SubclassA f) {updateTypeCount(typeof(SubclassA), f.GetType());};
		dispatcher.Add(subAFunc);

		System.Action<SubclassB> subBFunc = delegate(SubclassB f) {updateTypeCount(typeof(SubclassB), f.GetType());};
		dispatcher.Add(subBFunc);

		System.Action<StructA> structAFunc = delegate(StructA f) {updateTypeCount(typeof(StructA), f.GetType());};
		dispatcher.Add(structAFunc);

		System.Action<StructC> structCFunc = delegate(StructC f) {updateTypeCount(typeof(StructC), f.GetType());};
		dispatcher.Add(structCFunc);

		System.Action<IInterfaceA> intAFunc = delegate(IInterfaceA f) {updateTypeCount(typeof(IInterfaceA), f.GetType());};
		dispatcher.Add(intAFunc);

		System.Action<IInterfaceB> intBFunc = delegate(IInterfaceB f) {updateTypeCount(typeof(IInterfaceB), f.GetType());};
		dispatcher.Add(intBFunc);
            
		typeCounts.Clear();
		dispatcher.SendRaw(new BaseA());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new SubclassA());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 1);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new StructA());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 1);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new BaseB());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new SubclassB());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 1);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 1);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 1);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 0);

		typeCounts.Clear();
		dispatcher.SendRaw(new StructC());   
		Assert.AreEqual(getTypeCount(typeof(IInterfaceA)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseA)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassA)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructA)), 0);
		Assert.AreEqual(getTypeCount(typeof(IInterfaceB)), 0);
		Assert.AreEqual(getTypeCount(typeof(BaseB)), 0);
		Assert.AreEqual(getTypeCount(typeof(SubclassB)), 0);
		Assert.AreEqual(getTypeCount(typeof(StructC)), 1);
	}
}

}
#endif