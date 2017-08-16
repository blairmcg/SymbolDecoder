template<typename T, void (T::*)()> struct A
{
	void f() {}
};

int main()
{
	struct Base {
		void f(){}
	};
	// Note symbol for 2nd template fn pointer arg is main::Base::f
	A<Base, &(Base::f)> b;
	b.f();
	return 1;
}
