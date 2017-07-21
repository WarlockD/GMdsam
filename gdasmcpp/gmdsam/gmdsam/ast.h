#pragma once
#include "dsam.h"
#include <functional>



namespace gm {
	enum class GMCode
	{
		BadOp, // used for as a noop, mainly for branches hard jump location is after this
		Conv,
		Mul,
		Div,
		Rem,
		Mod,
		Add,
		Sub,
		And,
		Or,
		Xor,
		Neg,
		Not,
		Sal,
		Slt,
		Sle,
		Seq,
		Sne,
		Sge,
		Sgt,
		Pop,
		Dup,
		Ret,
		Exit,
		Popz,
		B,
		Bt,
		Bf,
		Pushenv,
		Popenv,
		Push,
		Call,
		Break,
		// special for IExpression
		Var,
		LogicAnd,
		LogicOr,
		LoopContinue,
		LoopOrSwitchBreak,
		Switch,
		Case,
		Constant,
		//     Assign,
		DefaultCase,
		Concat, // -- filler for lua or string math
		Array2D,
		Assign,
		CallUnresolved,
		Repeat, // The expression only holds the exit label, used for loopandcondition
				// AssignAdd,
				//    AssignSub,
				//   AssignMul,
				//  AssignDiv
				// there are more but meh
	};
	// visitor patern
	// https://stackoverflow.com/questions/11796121/implementing-the-visitor-pattern-using-c-templates
	// http://eli.thegreenplace.net/2011/05/17/the-curiously-recurring-template-pattern-in-c
	namespace ast {
		namespace priv {
			// Visitor template declaration
			template<typename... Types>
			class Visitor;

			// specialization for single type    
			template<typename T>
			class Visitor<T> {
			public:
				virtual void visit(T & visitable) = 0;
			};

			// specialization for multiple types
			template<typename T, typename... Types>
			class Visitor<T, Types...> : public Visitor<Types...> {
			public:
				// promote the function(s) from the base class
				using Visitor<Types...>::visit;

				virtual void visit(T & visitable) = 0;
			};

			template<typename... Types>
			class Visitable {
			public:
				virtual void accept(Visitor<Types...>& visitor) = 0;
			};

			template<typename Derived, typename... Types>
			class VisitableImpl : public Visitable<Types...> {
			public:
				virtual void accept(Visitor<Types...>& visitor) {
					visitor.visit(static_cast<Derived&>(*this));
				}
			};
		};
		// so we use enum to identify the type of node we are so we
		// don't have to use reinterpate_cast eveywhere
		class AstNode {

		public:
			AstNode(AstNode* p = nullptr) : _parent(p) {}
			AstNode* parent() { return _parent; }
			const AstNode* parent() const { return _parent; }
		protected:
			AstNode* _parent;
			void set_parent(AstNode* parent) { _parent = parent; }
			template<typename NODE_TYPE> friend class AstList;
		};
		template<typename NODE_TYPE>
		class AstList {
		public:
			static_assert(std::is_base_of<NODE_TYPE, AstNode>::value, "Must be a base of AstNode");

			using value_type = NODE_TYPE;
			using pointer = value_type*;
			using container = std::vector<pointer>;
			using iterator = typename container::iterator;
			using const_iterator = typename container::const_iterator;
			AstList(AstNode* p) : _parent(p) {}
			AstList(AstNode* p, container&& move) : _parent(p), _list(move) {}
			pointer at(size_t i) const { return _list.at(i); }
			void push_back(pointer v) {
				_set_list_parent(v);
				_list.push_back(v);
			}
			const_iterator insert(const_iterator position, pointer v) {
				_set_list_parent(v);
				return _list.insert(position, v);
			}
			const_iterator  remove(const_iterator position) {
				pointer v = *position;
				_clear_list_parent(v);
				return _list.remove(position);
			}
			void clear() {
				for (a : _list)
					_clear_list_parent(a);
				_list.clear();
			}
			const_iterator begin() const { return _list; }
			const_iterator end() const { return _list; }
		private:
			void _set_list_parent(pointer v) {
				assert(v->parent() == nullptr);
				v->set_parent(_parent);
			}
			void _clear_list_parent(pointer v) {
				assert(v->parent() == this);
				v->set_parent(nullptr);
			}
			AstNode* _parent;
			std::vector<pointer> _list;

		};
		template<typename NAME_TYPE>
		class NameEquality {
			const Symbol& __name() const { static_cast<NAME_TYPE&>(*this)._name; }
		public:
			const Symbol&  name() const { return _name(); }

			bool operator==(const Symbol& s) const { return __name() == s; }

			template<typename TT>
			bool operator==(const NameEquality<TT>& s) const { return __name() == s.__name(); }
			template<typename TT> bool operator!=(const TT& s) const { return !(*this == s); }
			size_t hash() const { return __name()._name.hash(); }
		};
		class Label : public NameEquality<Label> {
		protected:
			Symbol _name;
			size_t _offset;
		public:
			Label(Symbol s, size_t offset) : _name(s), _offset(offset) {}
			size_t offset() const { return _offset; }
			Symbol label() const { return _name; }
		};
		class UnresolvedVar : public NameEquality<UnresolvedVar> {
		protected:
			Symbol _name;
			int _extra;
			int _operand;
		public:
			UnresolvedVar(Symbol name, int extra, int operand) : _name(name), _extra(extra), _operand(operand) {}
			int extra() const { return _extra; }
			int operand() const { return _operand; }
			Symbol name() const { return _name; }
		};



#if 0
		class ILNode {
			ILNode* _parent;
			ILNode* _peer_next;
			ILNode** _peer_prev;
			// debug operations
			static inline void _check_head(const ILNode* head) {
				assert(head == nullptr || head->_peer_prev == &head);
			}
			static inline  void _check_next(const ILNode* elm) {
				assert(elm->_peer_next == nullptr || elm->_peer_next->_peer_prev == &elm->_peer_next);
			}
			static inline  void _check_prev(const ILNode* elm) {
				assert(*elm->_peer_prev == elm);
			}
			static void _insertAfter(ILNode* listelm, ILNode* elm) {
				_check_next(listelm);
				if ((elm->_peer_next = listelm->_peer_next) != nullptr)
					listelm->_peer_next->_peer_prev = &elm->_peer_next;
				listelm->_peer_next = elm;
				elm->_peer_prev = &listelm->_peer_next;
				elm->_parent = listelm->_parent;
			}
			static void _insertBefore(ILNode* listelm, ILNode* elm) {
				_check_prev(listelm);
				elm->_peer_prev = listelm->_peer_prev;
				elm->_peer_next = listelm;
				*listelm->_peer_prev = elm;
				listelm->_peer_prev = &elm->_peer_next;
				elm->_parent = listelm->_parent;
			}
			static void _insertChild(ILNode*& head, ILNode* parent, ILNode* elm) {
				_check_head(head);
				if ((elm->_peer_next = head) != nullptr)
					head->_peer_prev = &elm->_peer_next;
				head = elm;
				elm->_peer_prev = &head;
				elm->_parent = parent;
			}
			static void _removeChild(ILNode* elm) {
				_check_next(elm);
				_check_prev(elm);
				if (elm->_peer_next != nullptr)
					elm->_peer_next->_peer_prev = elm->_peer_prev;
				*elm->_peer_prev = elm->_peer_next;
				elm->_parent = nullptr;
			}
			static void _swapChildren(ILNode*& head1, ILNode*& head2) {
				std::swap(head1, head2);
				std::swap(head1->_parent, head2->_parent);
				if (head1 != nullptr) head1->_peer_prev = &head1;
				if (head2 != nullptr) head2->_peer_prev = &head2;
			}

		};
#endif

	};
}