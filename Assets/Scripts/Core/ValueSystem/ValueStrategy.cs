using UnityEngine;

/// <summary>
/// 数值策略基类
/// 
/// 约束：
/// 1.任何动态数值计算/公式都必须封装在继承自ValueStrategy的ScriptableObject中
/// 2.业务脚本只允许取上下文数据并交给策略求值，不允许出现具体数学公式
/// </summary>
public abstract class ValueStrategy : ScriptableObject
{
    //仅作为所有数值策略的统一基类与标识，不在此处强行规定Evaluate签名
    //由各业务域定义强类型Context与Strategy
}
