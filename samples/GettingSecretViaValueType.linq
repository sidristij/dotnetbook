<Query Kind="Program">
  <Connection>
    <ID>00e253b3-d172-4f5f-8499-9599dcc98237</ID>
    <Persist>true</Persist>
    <Driver Assembly="IQDriver" PublicKeyToken="5b59726538a49684">IQDriver.IQDriver</Driver>
    <Provider>Devart.Data.MySql</Provider>
    <CustomCxString>AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAffUP6cm5E0CcjkBeIkRs4AAAAAACAAAAAAAQZgAAAAEAACAAAADY7/tJdIdUmMF9zNY5cvIJIeVBnF9fxH0S83L637lYKQAAAAAOgAAAAAIAACAAAAA7XqL9WzIAVOC05Dv7IhQcl/t5zuLX2Ro3Us8ti+6Z0XAAAABYfhdLqrs8rN0fVeoxKj4eGzQynSdK4l6Bo9hB2LbcGkNCKVpoyFVdEiGcsTfMTZ2rifHPrtPQJi3UHHfTq/QGs0ACcLKd2MKzvXirb9Yma6N5t9trCX7HBUgJk3512hh8wcodc3tn7CW6fq+EH0T1QAAAABF7QA4Aa0thw0VaxgSctoX+Ejfx/LSesZUa5gnpJs7xYsoI4xs2rhRa0f2jWCRTJuSkj1n+huW44RG1hnPCOtA=</CustomCxString>
    <Server>mysql.alterf01.mass.hc.ru</Server>
    <Database>wwwalterfinru_privlib</Database>
    <UserName>alterf01_privlib</UserName>
    <Password>AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAAffUP6cm5E0CcjkBeIkRs4AAAAAACAAAAAAAQZgAAAAEAACAAAAD0nWLP0WamP1uvzXDcFv+sajLH5QtvZjatwlBxuDEE8AAAAAAOgAAAAAIAACAAAACvEX5qAaHg1ZkANGFKnzj1XVXaYNDcNBcJDikoJYs2PBAAAABW9AtuAw0y0O0xoFtiw8nsQAAAAMWYykCjmVGALnpNMwZF9xYYQGDnVSnT/Ed43UG+jCx3M1vRsg5fe6CtptyaU6+CguYM5PzaN8MFbGC65dlbRSY=</Password>
    <DisplayName>library</DisplayName>
    <EncryptCustomCxString>true</EncryptCustomCxString>
    <DriverData>
      <StripUnderscores>false</StripUnderscores>
      <QuietenAllCaps>false</QuietenAllCaps>
    </DriverData>
  </Connection>
</Query>

unsafe void Main()
{
	int secret2 = 999;
	Console.WriteLine("Entering FirstMethod");
	FirstMethod();
	Console.WriteLine("Returned from FirstMethod");
}

void FirstMethod()
{
	int secret = 666;
	Console.WriteLine("Entered FirstMethod");
	SecondMethod();
	Console.WriteLine("Returning from FirstMethod");
}

unsafe void SecondMethod()
{
	Console.WriteLine("Entered FirstMethod");
	StartingPoint sp;
	StackStructure ss;
	unsafe
	{
		ss = *(StackStructure*)&sp;
	}

	SecondMethod();
	Console.WriteLine("Returning from FirstMethod");
}

struct StackStructure
{
	public int a01_Self;
	public int a02_EBP_unsafe;
	public int a03_RET_unsafe;
	public int a04_RET_unsafe2;
	public int a05_RET;
	public int a06_EBP;
	public int a07_secret;
	public int a08_secret4;
	public int a09_secret5;
	public int a10_secret6;
	public int a11_EBP2;
	public int a12_RET2;
}

struct StartingPoint
{
	public int Self;
}