from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.mesh_facts import MeshFacts
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    *,
    path: str,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["path"] = path


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/mesh",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> MeshFacts | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = MeshFacts.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[MeshFacts | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    path: str,

) -> Response[MeshFacts | ProblemDetails]:
    """ The facts of a mesh file: one below the library, as a project lists it, or any file on this machine,
    so a
    client can say what a mesh is before it is brought in.

    Args:
        path (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[MeshFacts | ProblemDetails]
     """


    kwargs = _get_kwargs(
        path=path,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    path: str,

) -> MeshFacts | ProblemDetails | None:
    """ The facts of a mesh file: one below the library, as a project lists it, or any file on this machine,
    so a
    client can say what a mesh is before it is brought in.

    Args:
        path (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        MeshFacts | ProblemDetails
     """


    return sync_detailed(
        client=client,
path=path,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    path: str,

) -> Response[MeshFacts | ProblemDetails]:
    """ The facts of a mesh file: one below the library, as a project lists it, or any file on this machine,
    so a
    client can say what a mesh is before it is brought in.

    Args:
        path (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[MeshFacts | ProblemDetails]
     """


    kwargs = _get_kwargs(
        path=path,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    path: str,

) -> MeshFacts | ProblemDetails | None:
    """ The facts of a mesh file: one below the library, as a project lists it, or any file on this machine,
    so a
    client can say what a mesh is before it is brought in.

    Args:
        path (str):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        MeshFacts | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
path=path,

    )).parsed
