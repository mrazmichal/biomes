using UnityEngine;

/// <author>Michal Mráz</author>
public class DoublyLinkedCircularLinkedList<T>
{
    public Node<T> Head { get; private set; }
    public int Count { get; private set; }
    
    public DoublyLinkedCircularLinkedList()
    {
        Head = null;
        Count = 0;
    }

    public class Node<T>
    {
        public Node<T> next { get; internal set; }
        public Node<T> prev { get; internal set; }
        public T data { get; internal set; }
    }
    
    public Node<T> Add(T data)
    {
        Node<T> newNode = new Node<T>();
        newNode.data = data;
        
        if (Head == null)
        {
            newNode.next = newNode;
            newNode.prev = newNode;
            Head = newNode;
        }
        else
        {
            newNode.next = Head;
            newNode.prev = Head.prev;
            Head.prev.next = newNode;
            Head.prev = newNode;
        }

        Count++;
        return newNode;
    }

    public void Delete(Node<T> node)
    {
        if (node == null)
        {
            return;
        }

        // If there's only one node, clear the list
        if (node == node.next)
        {
            Head = null;
            Count = 0;
            return;
        }

        if (node == Head)
        {
            Head = Head.next;
        }
        
        node.next.prev = node.prev;
        node.prev.next = node.next;
        
        Count--;
    }

    public void Reverse()
    {
        if (Head == null)
        {
            return;
        }

        Head = Head.prev;
        Node<T> current = Head;
        do
        {
            Node<T> tmp = current.prev;
            current.next = current.prev;
            current = tmp;
        } while (current != Head);
        do
        {
            current.next.prev = current;
            current = current.next;
        } while (current != Head);

    }
    
    public void Clear()
    {
        Head = null;
        Count = 0;
    }
    
    public void Print()
    {
        if (Head == null)
        {
            return;
        }

        Node<T> current = Head;
        do
        {
            Debug.Log(current.data);
            current = current.next;
        } while (current != Head);
    }
    
    public void PrintBack()
    {
        if (Head == null)
        {
            return;
        }

        Node<T> current = Head;
        do
        {
            Debug.Log(current.data);
            current = current.prev;
        } while (current != Head);
    }
    
    public static void Test()
    {
        DoublyLinkedCircularLinkedList<int> list = new DoublyLinkedCircularLinkedList<int>();
        DoublyLinkedCircularLinkedList<int>.Node<int> node1 = list.Add(1);
        DoublyLinkedCircularLinkedList<int>.Node<int> node2 = list.Add(2);
        DoublyLinkedCircularLinkedList<int>.Node<int> node3 = list.Add(3);
        DoublyLinkedCircularLinkedList<int>.Node<int> node4 = list.Add(4);
        DoublyLinkedCircularLinkedList<int>.Node<int> node5 = list.Add(5);
        DoublyLinkedCircularLinkedList<int>.Node<int> node6 = list.Add(6);
        DoublyLinkedCircularLinkedList<int>.Node<int> node7 = list.Add(7);

        if (list.Count != 7)
        {
            throw new System.Exception("Test failed: list.Count != 7");
        }

        if (list.Head.data != 1)
        {
            throw new System.Exception("Test failed: list.Head.data != 1");
        }

        if (list.Head.next.data != 2)
        {
            throw new System.Exception("Test failed: list.Head.next.data != 2");
        }

        if (list.Head.prev.data != 7)
        {
            throw new System.Exception("Test failed: list.Head.prev.data != 7");
        }

        list.Delete(node7);

        if (list.Count != 6)
        {
            throw new System.Exception("Test failed: list.Count != 6");
        }

        if (list.Head.prev.data != 6)
        {
            throw new System.Exception("Test failed: list.Head.prev.data != 6");
        }

        if (list.Head.prev.prev.data != 5)
        {
            throw new System.Exception("Test failed: list.Head.prev.prev.data != 5");
        }
        
        list.Print();
        Debug.Log("Backwards:");
        list.PrintBack();
        Debug.Log("Backwards ended");

        list.Reverse();

        list.Print();
        Debug.Log("Backwards:");
        list.PrintBack();
        Debug.Log("Backwards ended");

        if (list.Head.data != 6)
        {
            throw new System.Exception("Test failed: list.Head.data != 6");
        }

        if (list.Head.prev.data != 1)
        {
            throw new System.Exception("Test failed: list.Head.prev.data != 1");
        }

        if (list.Head.next.data != 5)
        {
            throw new System.Exception("Test failed: list.Head.next.data != 5");
        }

        list.Clear();

        if (list.Count != 0)
        {
            throw new System.Exception("Test failed: list.Count != 0");
        }
    }

}

    

    
    
    
    
