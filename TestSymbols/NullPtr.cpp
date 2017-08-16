
std::nullptr_t f1(std::nullptr_t p1)
{
	return nullptr;
}

std::nullptr_t* f2(std::nullptr_t* p1)
{
	return p1;
}

std::nullptr_t** f3(std::nullptr_t** p1)
{
	return p1;
}

std::nullptr_t& f4(std::nullptr_t& p1)
{
	return p1;
}

std::nullptr_t*& f5(std::nullptr_t*& p1)
{
	return p1;
}

// ptr to ref is invalid
//std::nullptr_t&* f5a(std::nullptr_t&* p1)
//{
//	return p1;
//}

std::nullptr_t&& f6(std::nullptr_t&& p1)
{
	return 0;
}

std::nullptr_t*&& f7(std::nullptr_t*&& p1)
{
	return 0;
}

// Ptr to ref is invalid
//std::nullptr_t&&* f7a(std::nullptr_t&&* p1)
//{
//	return 0;
//}

std::nullptr_t volatile && f8(std::nullptr_t volatile && p1)
{
	return 0;
}

volatile int volatile && volatile f9(float && volatile p1)
{
	return 0;
}