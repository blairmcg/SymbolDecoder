namespace 
{
	struct A 
	{
		void f() { 5 * 6;  }
	};
}
void f()
{
	A a;
	a.f();
	struct B 
	{
		void f() {}
	};
}