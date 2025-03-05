from pinecone import Pinecone

pc = Pinecone("API-KEY")

query = "Tell me about the tech company known as Apple"

x = pc.inference.embed(
    model="llama-text-embed-v2",
    inputs=[query],
    parameters={
        "input_type": "query"
    }
)

results = index.query(
    namespace="ns1",
    vector=x[0].values,
    top_k=3,
    include_values=False,
    include_metadata=True
)

print(results)