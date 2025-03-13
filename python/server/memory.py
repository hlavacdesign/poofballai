# memory.py
#
# Handles Pinecone initialization and context retrieval logic.

import os
import json
from pinecone import Pinecone

class Memory:
    def __init__(self, pinecone_api_key: str, index_name: str, namespace: str):
        self.pc = Pinecone(pinecone_api_key)
        self.index = self.pc.Index(index_name)
        self.namespace = namespace

    def retrieve_context(self, user_message: str):
        """
        1) Embed user_message with Pinecone
        2) Query the Pinecone index
        3) Return the raw Pinecone results
        """
        try:
            # Embed user query
            x = self.pc.inference.embed(
                model="llama-text-embed-v2",
                inputs=[user_message],
                parameters={"input_type": "query"}
            )
            vec_length = len(x[0].values)
            print(f"[DEBUG] Embedded query vector length: {vec_length}")
            if vec_length > 5:
                print(f"[DEBUG] First 5 vector elements: {x[0].values[:5]}")

            # Query
            results = self.index.query(
                namespace=self.namespace,
                vector=x[0].values,
                top_k=3,
                include_values=False,
                include_metadata=True
            )

            # Convert QueryResponse to a dict for debug
            debug_dict = {
                "matches": [],
                "namespace": results.namespace
            }
            if results.matches is not None:
                for match in results.matches:
                    debug_dict["matches"].append({
                        "id": match.id,
                        "score": match.score,
                        "metadata": match.metadata
                    })
            print("[DEBUG] Pinecone query results (raw, as dict):")
            print(json.dumps(debug_dict, indent=2))

            return results

        except Exception as e:
            print("Pinecone retrieval error:", e)
            return None
