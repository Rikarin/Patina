from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.graph_mesh_maps_request import GraphMeshMapsRequest
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: GraphMeshMapsRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "put",
        "url": "/api/v1/graphs/{id}/mesh-maps".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Any | ProblemDetails | None:
    if response.status_code == 204:
        response_204 = cast(Any, None)
        return response_204

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[Any | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphMeshMapsRequest,

) -> Response[Any | ProblemDetails]:
    """ Chooses the folder of baked maps this graph's Mesh Map nodes read, and remembers it beside the file.

    Args:
        id (str):
        body (GraphMeshMapsRequest): Says which folder of baked maps a graph's Mesh Map nodes
            read.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[Any | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphMeshMapsRequest,

) -> Any | ProblemDetails | None:
    """ Chooses the folder of baked maps this graph's Mesh Map nodes read, and remembers it beside the file.

    Args:
        id (str):
        body (GraphMeshMapsRequest): Says which folder of baked maps a graph's Mesh Map nodes
            read.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Any | ProblemDetails
     """


    return sync_detailed(
        id=id,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphMeshMapsRequest,

) -> Response[Any | ProblemDetails]:
    """ Chooses the folder of baked maps this graph's Mesh Map nodes read, and remembers it beside the file.

    Args:
        id (str):
        body (GraphMeshMapsRequest): Says which folder of baked maps a graph's Mesh Map nodes
            read.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[Any | ProblemDetails]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: GraphMeshMapsRequest,

) -> Any | ProblemDetails | None:
    """ Chooses the folder of baked maps this graph's Mesh Map nodes read, and remembers it beside the file.

    Args:
        id (str):
        body (GraphMeshMapsRequest): Says which folder of baked maps a graph's Mesh Map nodes
            read.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Any | ProblemDetails
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
